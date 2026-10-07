using System.Security.Cryptography;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace AmorRP.Server.Features.Media;

public sealed class CurrencyIconService(AmorDbContext db, SessionAccess sessions, GroupAccess groups,
    CommandStore commands, IOptions<ServicePolicyOptions> policy)
{
    private static bool AnimatedContainer(byte[] input, SKEncodedImageFormat format)
    {
        // Skia may decode only the fallback PNG frame; reject APNG control chunks explicitly.
        if (format == SKEncodedImageFormat.Png)
        {
            for (var offset = 8; offset + 12 <= input.Length;)
            {
                var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(input.AsSpan(offset, 4));
                if (input.AsSpan(offset + 4, 4).SequenceEqual("acTL"u8)) return true;
                if (size > input.Length - offset - 12) break;
                offset += (int)size + 12;
            }
        }
        if (format == SKEncodedImageFormat.Webp)
        {
            for (var offset = 12; offset + 8 <= input.Length;)
            {
                var size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(offset + 4, 4));
                if (input.AsSpan(offset, 4).SequenceEqual("ANIM"u8) || input.AsSpan(offset, 4).SequenceEqual("ANMF"u8)) return true;
                if (size > input.Length - offset - 8) break;
                offset += (int)size + 8 + (int)(size % 2);
            }
        }
        return false;
    }
    public async Task<IResult> ReadAsync(HttpContext h, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; var (group, _) = await groups.RequireAsync(groupId, actor, ct);
        ApiFault.Require(group.CurrencyIconAssetId != null, 404, "not_found", "No custom currency icon.");
        var asset = await db.MediaAssets.SingleAsync(x => x.GroupId == groupId && x.Id == group.CurrencyIconAssetId, ct);
        h.Response.Headers.ETag = CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion);
        return Results.Bytes(asset.Png, "image/png");
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid groupId, bool upload, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        // Authorize before parsing untrusted media; authorize again under the write lock.
        var (initial, _) = await groups.RequireAsync(groupId, actor, ct); GroupAccess.Owner(initial, actor);
        byte[]? input = null; byte[]? png = null; int width = 0, height = 0;
        if (upload)
        {
            var limit = policy.Value.MaxCurrencyIconBytes;
            var feature = h.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = limit + 8192;
            ApiFault.Require(h.Request.HasFormContentType && (!h.Request.ContentLength.HasValue || h.Request.ContentLength <= limit + 8192), 413, "image_too_large", "Currency icon upload is too large or not multipart data.");
            IFormCollection form;
            try { form = await h.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = limit + 8192, MemoryBufferThreshold = limit + 8192, ValueCountLimit = 1, MultipartHeadersLengthLimit = 4096 }, ct); }
            catch (InvalidDataException) { throw new ApiFault(413, "image_too_large", "Currency icon upload exceeds its limits."); }
            ApiFault.Require(form.Files.Count == 1 && form.Files[0].Name == "file" && form.Count == 0, 422, "invalid_image", "Upload one image file.");
            var file = form.Files[0]; ApiFault.Require(file.Length > 0 && file.Length <= limit, 413, "image_too_large", "Currency icon exceeds the encoded-file limit.");
            using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct); input = memory.ToArray();
            using var data = SKData.CreateCopy(input); using var codec = SKCodec.Create(data);
            ApiFault.Require(codec != null && codec.EncodedFormat is SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp, 415, "unsupported_media_type", "Use a static PNG, JPG or WebP image.");
            ApiFault.Require(!AnimatedContainer(input, codec!.EncodedFormat), 422, "invalid_image", "Animated images cannot be currency icons.");
            width = codec.Info.Width; height = codec.Info.Height;
            ApiFault.Require(width is > 0 and <= 128 && height is > 0 and <= 128 && codec.FrameCount <= 1, 422, "invalid_image", "Use a static image at most 128 pixels on each side.");
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            ApiFault.Require(codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success, 422, "invalid_image", "Image is malformed or incomplete.");
            using var normalized = bitmap.Encode(SKEncodedImageFormat.Png, 100); png = normalized.ToArray();
            ApiFault.Require(png.Length <= 131072, 422, "invalid_image", "Normalized image exceeds storage bounds.");
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CommandStore.LockAsync(db, ["character:" + actor, "group:" + groupId], ct);
        // The preflight may have tracked an outdated owner/version; reload under the lock.
        await db.Entry(initial).ReloadAsync(ct);
        var (group, _) = await groups.RequireAsync(groupId, actor, ct); GroupAccess.Owner(group, actor);
        object? body = input == null ? null : new { contentHash = Convert.ToHexString(SHA256.HashData(input)) };
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion));
        var previous = group.CurrencyIconAssetId;
        if (png != null)
        {
            var asset = new MediaAssetRow { GroupId = groupId, Png = png, Width = width, Height = height, ContentHash = Convert.ToHexString(SHA256.HashData(png)) };
            db.MediaAssets.Add(asset); group.CurrencyIconAssetId = asset.Id;
        }
        else group.CurrencyIconAssetId = null;
        group.CurrencyVersion++;
        var op = commands.New(h, scope, body, actor, groupId, upload ? "currency.icon.replace" : "currency.icon.remove"); op.Summary = upload ? "Updated currency icon." : "Removed custom currency icon.";
        h.Response.Headers.ETag = CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion);
        var result = await commands.SaveAsync(h, op, new CommandResult<AmorRP.Contracts.Currency.Currency>(op.Id, GroupAccess.Currency(group)), ct);
        if (previous.HasValue) { await db.MediaAssets.Where(x => x.GroupId == groupId && x.Id == previous).ExecuteDeleteAsync(ct); }
        await tx.CommitAsync(ct); return result;
    }
}
