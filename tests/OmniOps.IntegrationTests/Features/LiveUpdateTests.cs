using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace OmniOps.IntegrationTests.Features;

/// <summary>The office sees changes as they happen, and only its own company's.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class LiveUpdateTests(ApiFixture fixture)
{
    [Fact]
    public async Task ADriversPosition_ReachesTheirOfficeLive_AndNoOtherCompany()
    {
        var company = await fixture.NewCompanyAsync();
        var other = await fixture.NewCompanyAsync("Other Haulage");
        await using var office = await ListenAsync(company);
        await using var outsider = await ListenAsync(other);

        var jobId = (await company.CreateJobAsync()).GetProperty("id").GetGuid();
        Assert.Equal(jobId, await office.NextAsync());

        (await company.Owner.PostAsync($"/api/v1/driver/jobs/{jobId}/start", null)).EnsureSuccessStatusCode();
        Assert.Equal(jobId, await office.NextAsync());

        (await company.Owner.PostAsJsonAsync($"/api/v1/driver/jobs/{jobId}/locations", new
        {
            points = new[] { new { id = Guid.NewGuid(), latitude = 41.9, longitude = 21.4, recordedAtUtc = DateTime.UtcNow } },
        })).EnsureSuccessStatusCode();
        Assert.Equal(jobId, await office.NextAsync());

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken); // anything misrouted has arrived by now
        Assert.False(outsider.Received, "Another company was told about this company's job.");
    }

    [Fact]
    public async Task TheLiveChannel_RefusesCallersWhoAreNotSignedIn()
    {
        var connection = Connect(accessToken: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync(TestContext.Current.CancellationToken));
    }

    private async Task<Listener> ListenAsync(Company company)
    {
        var connection = Connect(company.Session.GetProperty("accessToken").GetString());
        var listener = new Listener(connection);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return listener;
    }

    private HubConnection Connect(string? accessToken) => new HubConnectionBuilder()
        .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, "/hubs/live"), options =>
        {
            options.Transports = HttpTransportType.LongPolling; // the test server has no WebSockets
            options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult(accessToken);
        })
        .Build();

    private sealed class Listener : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly Channel<Guid> _changes = Channel.CreateUnbounded<Guid>();

        public Listener(HubConnection connection)
        {
            _connection = connection;
            connection.On<JobChanged>("job-changed", change => _changes.Writer.TryWrite(change.JobId));
        }

        public bool Received => _changes.Reader.Count > 0;

        public async Task<Guid> NextAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await _changes.Reader.ReadAsync(timeout.Token);
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed record JobChanged(Guid JobId);
}
