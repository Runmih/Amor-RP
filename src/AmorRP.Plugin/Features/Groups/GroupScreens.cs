using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private void DrawGroups()
    {
        ImGui.TextUnformatted($"Joined groups: {groups.Length} (owned groups also occupy a joined slot)");
        if (group != null)
        {
            ImGui.Separator(); ImGui.TextUnformatted(group.Name); Wrapped(group.Description);
            ImGui.TextUnformatted($"Your currency: {group.MyBalance.Available} {group.Currency.Name}");
            if (group.MyTradingRestricted) ImGui.TextUnformatted("Trading is restricted for you in this group.");
            if (!Owner && ImGui.Button("Leave this group")) Mutate("POST", $"api/v1/groups/{group.Id}/leave", null);
            if (transfer?.ToCharacterId == session!.Character.Id && transfer.Status == "proposed")
            {
                Wrapped($"Ownership offered to you. Expires {transfer.ExpiresAt.LocalDateTime:g}.");
                if (ImGui.Button("Accept ownership")) Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers/{transfer.Id}/accept", null);
                ImGui.SameLine(); if (ImGui.Button("Decline ownership")) Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers/{transfer.Id}/cancel", null);
            }
        }
        if (ImGui.CollapsingHeader("Join a group"))
        {
            ImGui.InputText("Invitation code", ref invitationCode, 100);
            Wrapped("Joining shares your verified character name, home world and membership with the group. Owners see management summaries and administrative history.");
            ImGui.Checkbox("I accept group member visibility", ref visibilityAccepted);
            ImGui.BeginDisabled(!visibilityAccepted || string.IsNullOrWhiteSpace(invitationCode));
            if (ImGui.Button("Join")) Mutate("POST", "api/v1/groups/join", new JoinGroup(invitationCode.Trim(), visibilityAccepted));
            ImGui.EndDisabled();
        }
        if (ImGui.CollapsingHeader("Create a group"))
        {
            ImGui.InputText("Group name", ref name, 256); ImGui.InputTextMultiline("Description", ref description, 8192, new(0,65));
            ImGui.InputText("Currency name", ref currencyName, 256); Wrapped("A default coin icon is used until you upload a custom icon in owner settings.");
            ImGui.InputInt("Initial potion points / character / week", ref initialPoints); ImGui.InputInt("Initial letters / character / week", ref initialLetters);
            Wrapped("These limits establish your first week's policy. Later policy edits start next Monday.");
            if (initialPoints == 0) Wrapped("Zero potion points means no potion production in the first week.");
            if (ImGui.Button("Create group")) Mutate("POST", "api/v1/groups", new GroupCreate(name, description, currencyName, symbol, initialPoints, initialLetters));
        }
    }
    private Member? SelectedMember => members.FirstOrDefault(x => x.Character.Id == memberSelection);
    private void DrawMembers()
    {
        if (group == null) { Wrapped("Join or create a group first."); return; }
        if (Owner && ImGui.Checkbox("Show inactive members", ref showInactive)) { members = []; memberSelection = null; refreshNeeded = true; }
        foreach (var row in members)
        {
            if (ImGui.Selectable($"{row.Character.DisplayName} @ {row.Character.HomeWorldName} — {row.Status}{(row.IsOwner ? " (owner)" : "")}###member{row.Character.Id}", memberSelection == row.Character.Id))
            {
                memberSelection = row.Character.Id; memberDraftEtag = row.Etag; grantCoins = row.Capabilities.Contains("currency.manage");
                grantPotions = row.Capabilities.Contains("items.potion.create"); grantRemoval = row.Capabilities.Contains("inventory.remove"); restricted = row.TradingRestricted; reason = ""; removalReason = ""; restrictionReason = ""; confirmRemoval = false;
            }
        }
        if (membersCursor != null && ImGui.Button("Load more members"))
        {
            var cursor = membersCursor; var id = group.Id; var token = session!.AccessToken; var owner = Owner && showInactive;
            Start(async ct => { var page = await client.GetAsync<MemberPage>(origin!, $"api/v1/groups/{id}/members?cursor={Uri.EscapeDataString(cursor)}" + (owner ? "&includeDormant=true" : ""), token, ct);
                return () => { members = members.Concat(page.Items).DistinctBy(x => x.Character.Id).ToArray(); membersCursor = page.NextCursor; status = "Members loaded."; }; }, "Loading more members…");
        }
        var target = SelectedMember;
        if (target == null) { Wrapped("Select a member to see authorized actions."); return; }
        ImGui.Separator(); ImGui.TextUnformatted($"Selected: {target.Character.DisplayName}");
        if (memberDraftEtag != target.Etag) Wrapped("This member changed since selection. Select the row again to review current authorizations before editing.");
        ImGui.TextUnformatted("Permissions: " + (target.Capabilities.Length == 0 ? "ordinary member" : string.Join(", ", target.Capabilities)));
        if (target.TradingRestricted) Wrapped("Trading restricted in this group.");
        if (CanCoins && target.Status == "active")
        {
            ImGui.InputText("Signed currency change (e.g. 10 or -5)", ref delta, 32); ImGui.InputText("Reason", ref reason, 2048);
            if (ImGui.Button("Apply currency adjustment")) Mutate("POST", $"api/v1/groups/{group.Id}/currency/adjustments", new CurrencyAdjustment(target.Character.Id, delta, reason));
            ImGui.SameLine();
            if (ImGui.Button("Read selected balance"))
            {
                var path = $"api/v1/groups/{group.Id}/members/{target.Character.Id}/balances"; var token = session!.AccessToken;
                Start(async ct => { var balance = await client.GetAsync<BalancePage>(origin!, path, token, ct); return () => status = $"Selected balance: {balance.Items[0].Available} available; {balance.Items[0].Reserved} reserved."; }, "Reading balance…");
            }
        }
        DrawMemberInventory(target);
        if (!Owner || target.IsOwner) return;
        if (target.Status == "active")
        {
            ImGui.Separator(); ImGui.TextUnformatted("Individual action authorizations");
            ImGui.Checkbox("Manage currency", ref grantCoins); ImGui.Checkbox("Create potion copies", ref grantPotions); ImGui.Checkbox("Remove other members' items", ref grantRemoval);
            if (ImGui.Button("Save authorizations"))
            {
                var actions = new List<string>(); if (grantCoins) actions.Add("currency.manage"); if (grantPotions) actions.Add("items.potion.create"); if (grantRemoval) actions.Add("inventory.remove");
                Mutate("PUT", $"api/v1/groups/{group.Id}/members/{target.Character.Id}/capabilities", new MemberCapabilities(actions.ToArray()), memberDraftEtag ?? target.Etag);
            }
            ImGui.Checkbox("Restrict trading", ref restricted);
            ImGui.InputText("Trading restriction reason", ref restrictionReason, 2048);
            if (ImGui.Button("Save trading restriction")) Mutate("PUT", $"api/v1/groups/{group.Id}/members/{target.Character.Id}/trade-restriction", new MemberRestriction(restricted, restrictionReason), memberDraftEtag ?? target.Etag);
            ImGui.Separator(); ImGui.InputText("Member removal reason (required)", ref removalReason, 2048);
            ImGui.Checkbox("Confirm blocking this member; their assets are retained", ref confirmRemoval);
            ImGui.BeginDisabled(!confirmRemoval || string.IsNullOrWhiteSpace(removalReason));
            if (ImGui.Button("Remove and block member")) Mutate("POST", $"api/v1/groups/{group.Id}/members/{target.Character.Id}/removal", new MemberRemoval(removalReason), memberDraftEtag ?? target.Etag);
            ImGui.EndDisabled();
            if (ImGui.Button("Propose ownership transfer")) Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers", new OwnershipProposal(target.Character.Id));
        }
        else if (target.Status == "blocked")
        {
            ImGui.InputText("Restoration reason", ref reason, 2048);
            Wrapped("Restoration allows joining again with a valid invitation. It does not grant membership or permissions.");
            if (ImGui.Button("Restore eligibility")) Mutate("POST", $"api/v1/groups/{group.Id}/members/{target.Character.Id}/restoration", new MemberRemoval(reason), memberDraftEtag ?? target.Etag);
        }
    }
    private static string ETag(string kind, Guid id, int version) => $"\"{kind}:{id:N}:{version}\"";
    private void DrawOwner()
    {
        if (group == null) { Wrapped("Select a group first."); return; }
        if (policies != null)
        {
            ImGui.TextUnformatted($"Current weekly limits: {policies.Current.PotionPoints} potion points, {policies.Current.Letters} letters per character.");
            Wrapped($"Next reset: {policies.NextResetAt.LocalDateTime:g}. Scheduled limits: {policies.Next.PotionPoints} points, {policies.Next.Letters} letters.");
        }
        if (!Owner) { Wrapped("Only the group owner manages these settings."); return; }
        if (ImGui.CollapsingHeader("Weekly policy"))
        {
            ImGui.InputInt("Next week's potion points", ref nextPoints); ImGui.InputInt("Next week's letters", ref nextLetters);
            if (policies != null && ImGui.Button("Reload scheduled policy")) { nextPoints = policies.Next.PotionPoints; nextLetters = policies.Next.Letters; policyDraftVersion = policies.Version; }
            if (policies != null && policyDraftVersion != policies.Version) Wrapped("Policy changed while editing. Reload scheduled policy before saving.");
            if (policies != null && ImGui.Button("Schedule weekly limits")) Mutate("PUT", $"api/v1/groups/{group.Id}/policies", new PolicyEdit(nextPoints, nextLetters), ETag("policy", group.Id, policyDraftVersion));
        }
        if (ImGui.CollapsingHeader("Group and currency names"))
        {
            if (ImGui.Button("Load current names into editor")) { editName = group.Name; editDescription = group.Description; editCurrencyName = group.Currency.Name; editGroupVersion = group.Version; editCurrencyVersion = group.Currency.Version; }
            ImGui.InputText("Name", ref editName, 256); ImGui.InputTextMultiline("Group description", ref editDescription, 8192, new(0,65));
            if (ImGui.Button("Save group details")) Mutate("PATCH", $"api/v1/groups/{group.Id}", new GroupEdit(editName, editDescription), ETag("group", group.Id, editGroupVersion));
            ImGui.InputText("Coin name", ref editCurrencyName, 256);
            if (ImGui.Button("Save currency details")) Mutate("PATCH", $"api/v1/groups/{group.Id}/currency", new CurrencyEdit(editCurrencyName, group.Currency.Symbol), ETag("currency", group.Currency.Id, editCurrencyVersion));
        }
        DrawCurrencyIconEditor();
        DrawCatalogEditor();
        if (ImGui.CollapsingHeader("Invitations"))
        {
            ImGui.InputInt("Valid for hours (1–168)", ref inviteHours); ImGui.InputInt("Maximum uses (1–100)", ref inviteUses);
            if (ImGui.Button("Create invitation")) Mutate("POST", $"api/v1/groups/{group.Id}/invitations", new InvitationCreate(inviteHours, inviteUses));
            if (createdCode.Length > 0) { Wrapped("Share this code with invited players. It is shown only for the creation response."); ImGui.InputText("New invitation code", ref createdCode, 100, ImGuiInputTextFlags.ReadOnly); }
            foreach (var invite in invitations)
            {
                ImGui.PushID(invite.Id.ToString());
                Wrapped($"{invite.Used}/{invite.MaxUses} uses; expires {invite.ExpiresAt.LocalDateTime:g}{(invite.Revoked ? " — revoked" : "")}");
                if (!invite.Revoked && ImGui.Button("Revoke invitation")) Mutate("DELETE", $"api/v1/groups/{group.Id}/invitations/{invite.Id}", null);
                ImGui.PopID();
            }
            if (invitationsCursor != null && ImGui.Button("Load more invitations"))
            {
                var path = $"api/v1/groups/{group.Id}/invitations?cursor={Uri.EscapeDataString(invitationsCursor)}"; var token = session!.AccessToken;
                Start(async ct => { var page = await client.GetAsync<InvitationPage>(origin!, path, token, ct); return () => { invitations = [..invitations, ..page.Items]; invitationsCursor = page.NextCursor; }; }, "Loading invitations…");
            }
        }
        if (transfer is { Status: "proposed" })
        {
            Wrapped($"Pending ownership transfer expires {transfer.ExpiresAt.LocalDateTime:g}.");
            if (ImGui.Button("Cancel ownership proposal")) Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers/{transfer.Id}/cancel", null);
        }
        if (ImGui.CollapsingHeader("Delete group"))
        {
            Wrapped("Deletion makes this group's currency and future items inaccessible to all members. Confirm its exact name.");
            ImGui.InputText("Exact group name", ref deleteConfirmation, 256);
            ImGui.BeginDisabled(deleteConfirmation != group.Name);
            if (ImGui.Button("Delete this group")) Mutate("POST", $"api/v1/groups/{group.Id}/deletion", new GroupDeletion(deleteConfirmation), ETag("group", group.Id, group.Version));
            ImGui.EndDisabled();
        }
    }
    private void DrawHistory()
    {
        if (group != null)
        {
            ImGui.TextUnformatted(Owner ? "Group administrative history" : "Your group history");
            foreach (var row in history)
            { Wrapped($"{row.OccurredAt.LocalDateTime:g}: {row.Summary}"); if (row.Reason != null) Wrapped("Reason: " + row.Reason); }
            if (historyCursor != null && ImGui.Button("Load more history"))
            {
                var path = $"api/v1/groups/{group.Id}/{(Owner ? "audit" : "history")}?cursor={Uri.EscapeDataString(historyCursor)}"; var token = session!.AccessToken;
                Start(async ct => { var page = await client.GetAsync<HistoryEntryPage>(origin!, path, token, ct); return () => { history = [..history, ..page.Items]; historyCursor = page.NextCursor; }; }, "Loading history…");
            }
        }
        ImGui.Separator(); ImGui.TextUnformatted("Saved device sessions for this character");
        foreach (var device in devices)
        {
            ImGui.PushID(device.Id.ToString());
            Wrapped($"Created {device.CreatedAt.LocalDateTime:g}; renewed {device.LastUsedAt.LocalDateTime:g}{(device.IsCurrent ? " — this device" : "")}");
            if (!device.IsCurrent && ImGui.Button("Revoke device")) Mutate("DELETE", $"api/v1/me/sessions/{device.Id}", null);
            ImGui.PopID();
        }
    }
}
