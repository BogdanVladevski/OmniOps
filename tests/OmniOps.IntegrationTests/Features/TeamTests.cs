namespace OmniOps.IntegrationTests.Features;

/// <summary>Inviting office staff, removing them, and resetting or changing passwords.</summary>
[Collection(ApiTestGroup.Name)]
public class TeamTests(ApiFixture fixture)
{
    private const string Password = Company.Password;

    /// <summary>Links open the app at /join/{token} or /reset/{token}.</summary>
    private static string TokenFrom(string link, string page)
    {
        var path = new Uri(link).AbsolutePath;
        Assert.StartsWith($"/{page}/", path, StringComparison.Ordinal);
        return Uri.UnescapeDataString(path[(page.Length + 2)..]);
    }

    private static Task<JsonElement> JsonAsync(HttpResponseMessage response) => response.ReadJsonAsync();

    /// <summary>Invites and accepts; returns the new person's session.</summary>
    private async Task<(HttpClient Client, JsonElement Session, string Email)> AddTeammateAsync(Company company, string role)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var invitation = await JsonAsync(await company.Owner.PostAsJsonAsync("/api/v1/account/team/invitations", new { email, role }));
        Assert.False(invitation.GetProperty("emailed").GetBoolean()); // no mail server in tests: the owner shares the link
        var token = TokenFrom(invitation.GetProperty("link").GetString()!, "join");
        var session = await JsonAsync(await fixture.Anonymous().PostAsJsonAsync(
            $"/api/v1/account/invitations/{token}/accept", new { name = "New Person", password = Password }));
        return (fixture.SignedIn(session.GetProperty("accessToken").GetString()!), session, email);
    }

    [Fact]
    public async Task InvitedDispatcher_JoinsTheCompany_WithOfficeButNotOwnerAccess()
    {
        var company = await fixture.NewCompanyAsync("Invite Haulage");
        var email = $"{Guid.NewGuid():N}@example.test";
        var invitation = await JsonAsync(await company.Owner.PostAsJsonAsync("/api/v1/account/team/invitations",
            new { email = email.ToUpperInvariant(), role = "dispatcher" }));
        var token = TokenFrom(invitation.GetProperty("link").GetString()!, "join");

        var anonymous = fixture.Anonymous();
        var preview = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/account/invitations/{token}");
        Assert.Equal("Invite Haulage", preview.GetProperty("companyName").GetString());
        Assert.Equal(email, preview.GetProperty("email").GetString());

        var team = await company.Owner.GetFromJsonAsync<JsonElement>("/api/v1/account/team");
        Assert.Contains(team.GetProperty("invitations").EnumerateArray(), i => i.GetProperty("email").GetString() == email);

        var session = await JsonAsync(await anonymous.PostAsJsonAsync($"/api/v1/account/invitations/{token}/accept",
            new { name = "Dana", password = Password }));
        var account = session.GetProperty("account");
        Assert.Equal("Dispatcher", account.GetProperty("role").GetString());
        Assert.Equal(company.Session.GetProperty("account").GetProperty("organizationId").GetGuid(), account.GetProperty("organizationId").GetGuid());

        var dispatcher = fixture.SignedIn(session.GetProperty("accessToken").GetString()!);
        (await dispatcher.GetAsync("/api/v1/jobs")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.GetAsync("/api/v1/account/team")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dispatcher.PostAsJsonAsync("/api/v1/account/team/invitations",
            new { email = "x@example.test", role = "owner" })).StatusCode);

        // The link works once, and they can now sign in normally.
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/account/invitations/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync($"/api/v1/account/invitations/{token}/accept",
            new { name = "Again", password = Password })).StatusCode);
        (await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email, password = Password })).EnsureSuccessStatusCode();

        team = await company.Owner.GetFromJsonAsync<JsonElement>("/api/v1/account/team");
        Assert.Empty(team.GetProperty("invitations").EnumerateArray());
        Assert.Contains(team.GetProperty("members").EnumerateArray(), m => m.GetProperty("email").GetString() == email);
    }

    [Fact]
    public async Task AnotherCompany_CannotSeeOrTouchTheTeam()
    {
        var company = await fixture.NewCompanyAsync("Team Owner Co");
        var other = await fixture.NewCompanyAsync("Nosy Co");
        var (_, session, email) = await AddTeammateAsync(company, "Dispatcher");
        var userId = session.GetProperty("account").GetProperty("userId").GetGuid();
        var pending = await JsonAsync(await company.Owner.PostAsJsonAsync("/api/v1/account/team/invitations",
            new { email = $"{Guid.NewGuid():N}@example.test", role = "Owner" }));
        var invitationId = pending.GetProperty("id").GetGuid();

        var otherTeam = await other.Owner.GetStringAsync("/api/v1/account/team");
        Assert.DoesNotContain(email, otherTeam);
        Assert.DoesNotContain(invitationId.ToString(), otherTeam);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.DeleteAsync($"/api/v1/account/team/invitations/{invitationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.DeleteAsync($"/api/v1/account/team/{userId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.PutAsJsonAsync($"/api/v1/account/team/{userId}/role", new { role = "Owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.PostAsync($"/api/v1/account/team/{userId}/reset-link", null)).StatusCode);

        // The owner can withdraw it; the link then stops working.
        (await company.Owner.DeleteAsync($"/api/v1/account/team/invitations/{invitationId}")).EnsureSuccessStatusCode();
        var token = TokenFrom(pending.GetProperty("link").GetString()!, "join");
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Anonymous().GetAsync($"/api/v1/account/invitations/{token}")).StatusCode);
    }

    [Fact]
    public async Task RemovedTeammate_IsSignedOutAtOnce()
    {
        var company = await fixture.NewCompanyAsync("Remove Co");
        var (client, session, email) = await AddTeammateAsync(company, "Dispatcher");
        (await client.GetAsync("/api/v1/jobs")).EnsureSuccessStatusCode();

        var userId = session.GetProperty("account").GetProperty("userId").GetGuid();
        (await company.Owner.DeleteAsync($"/api/v1/account/team/{userId}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/jobs")).StatusCode);
        var anonymous = fixture.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = session.GetProperty("refreshToken").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/sign-in",
            new { email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task ACompany_AlwaysKeepsAnOwner()
    {
        var company = await fixture.NewCompanyAsync("Last Owner Co");
        var ownerId = company.Session.GetProperty("account").GetProperty("userId").GetGuid();

        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PutAsJsonAsync($"/api/v1/account/team/{ownerId}/role",
            new { role = "Dispatcher" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.DeleteAsync($"/api/v1/account/team/{ownerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await company.Owner.PutAsJsonAsync($"/api/v1/account/team/{ownerId}/role",
            new { role = "Driver" })).StatusCode);

        // With a second owner, the first may step down; their old sign-in ends and the new one has the new role.
        var (second, _, _) = await AddTeammateAsync(company, "Owner");
        (await second.GetAsync("/api/v1/account/team")).EnsureSuccessStatusCode();
        (await company.Owner.PutAsJsonAsync($"/api/v1/account/team/{ownerId}/role", new { role = "Dispatcher" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.Owner.GetAsync("/api/v1/account/team")).StatusCode);

        var again = await JsonAsync(await fixture.Anonymous().PostAsJsonAsync("/api/v1/account/sign-in",
            new { email = company.Email, password = Password }));
        Assert.Equal("Dispatcher", again.GetProperty("account").GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.SignedIn(again.GetProperty("accessToken").GetString()!).GetAsync("/api/v1/account/team")).StatusCode);
    }

    [Fact]
    public async Task PasswordResetLink_WorksOnce_AndEndsOldSignIns()
    {
        var company = await fixture.NewCompanyAsync("Reset Co");
        var (client, session, email) = await AddTeammateAsync(company, "Dispatcher");
        var userId = session.GetProperty("account").GetProperty("userId").GetGuid();

        var reset = await JsonAsync(await company.Owner.PostAsync($"/api/v1/account/team/{userId}/reset-link", null));
        var token = TokenFrom(reset.GetProperty("link").GetString()!, "reset");

        var anonymous = fixture.Anonymous();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/v1/account/reset-password",
            new { token, password = "short" })).StatusCode);
        var renewed = await JsonAsync(await anonymous.PostAsJsonAsync("/api/v1/account/reset-password",
            new { token, password = "a brand new passphrase" }));
        (await fixture.SignedIn(renewed.GetProperty("accessToken").GetString()!).GetAsync("/api/v1/jobs")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/api/v1/account/reset-password",
            new { token, password = "another new passphrase" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/account/sign-in",
            new { email, password = Password })).StatusCode);
        (await anonymous.PostAsJsonAsync("/api/v1/account/sign-in", new { email, password = "a brand new passphrase" })).EnsureSuccessStatusCode();

        // An invitation token is not a reset token.
        var invitation = await JsonAsync(await company.Owner.PostAsJsonAsync("/api/v1/account/team/invitations",
            new { email = $"{Guid.NewGuid():N}@example.test", role = "Dispatcher" }));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/api/v1/account/reset-password",
            new { token = TokenFrom(invitation.GetProperty("link").GetString()!, "join"), password = "a brand new passphrase" })).StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_GivesTheSameAnswer_ForKnownAndUnknownEmails()
    {
        var company = await fixture.NewCompanyAsync("Forgot Co");
        var anonymous = fixture.Anonymous();
        var known = await anonymous.PostAsJsonAsync("/api/v1/account/forgot-password", new { email = company.Email });
        var unknown = await anonymous.PostAsJsonAsync("/api/v1/account/forgot-password", new { email = "nobody-here@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChangingPassword_NeedsTheCurrentOne_AndSignsOutOtherDevices()
    {
        var company = await fixture.NewCompanyAsync("Change Co");
        var otherDevice = await JsonAsync(await fixture.Anonymous().PostAsJsonAsync("/api/v1/account/sign-in",
            new { email = company.Email, password = Password }));
        var laptop = fixture.SignedIn(otherDevice.GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.Conflict, (await company.Owner.PostAsJsonAsync("/api/v1/account/password",
            new { currentPassword = "not my password", newPassword = "a brand new passphrase" })).StatusCode);
        (await laptop.GetAsync("/api/v1/jobs")).EnsureSuccessStatusCode();

        var fresh = await JsonAsync(await company.Owner.PostAsJsonAsync("/api/v1/account/password",
            new { currentPassword = Password, newPassword = "a brand new passphrase" }));
        (await fixture.SignedIn(fresh.GetProperty("accessToken").GetString()!).GetAsync("/api/v1/jobs")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/v1/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.Owner.GetAsync("/api/v1/jobs")).StatusCode);
    }

    [Fact]
    public async Task SignOutEverywhere_EndsEverySession()
    {
        var company = await fixture.NewCompanyAsync("Everywhere Co");
        (await company.Owner.PostAsync("/api/v1/account/sign-out-everywhere", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.Owner.GetAsync("/api/v1/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Anonymous().PostAsJsonAsync("/api/v1/account/refresh",
            new { refreshToken = company.Session.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Fact]
    public async Task OwnerCanRenameTheCompany_AndTheirProfile()
    {
        var company = await fixture.NewCompanyAsync("Old Name Co");
        var renamed = await JsonAsync(await company.Owner.PutAsJsonAsync("/api/v1/account/company", new { name = "New Name Co" }));
        Assert.Equal("New Name Co", renamed.GetProperty("name").GetString());
        var me = await JsonAsync(await company.Owner.PutAsJsonAsync("/api/v1/account/me", new { displayName = "Ana Petrova" }));
        Assert.Equal("Ana Petrova", me.GetProperty("displayName").GetString());
        Assert.Equal("New Name Co", me.GetProperty("organizationName").GetString());
    }
}
