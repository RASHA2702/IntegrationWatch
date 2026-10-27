using IntegrationWatch.Core.Entities;
using IntegrationWatch.Core.Enums;
using IntegrationWatch.Core.Interfaces;
using IntegrationWatch.Infrastructure.Background;
using IntegrationWatch.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace IntegrationWatch.Tests.Infrastructure;

[TestClass]
public class ProbeWorkerTests
{
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<IMonitoredEndpointRepository> _endpointRepoMock = null!;
    private Mock<IProbeExecutionRepository> _probeRepoMock = null!;
    private Mock<IIncidentRepository> _incidentRepoMock = null!;
    private Mock<IAlertDispatcher> _alertDispatcherMock = null!;
    private Mock<IResilientHttpClient> _httpClientMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _endpointRepoMock = new Mock<IMonitoredEndpointRepository>();
        _probeRepoMock = new Mock<IProbeExecutionRepository>();
        _incidentRepoMock = new Mock<IIncidentRepository>();
        _alertDispatcherMock = new Mock<IAlertDispatcher>();
        _httpClientMock = new Mock<IResilientHttpClient>();

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitoredEndpointRepository))).Returns(_endpointRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IProbeExecutionRepository))).Returns(_probeRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IIncidentRepository))).Returns(_incidentRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IAlertDispatcher))).Returns(_alertDispatcherMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IResilientHttpClient))).Returns(_httpClientMock.Object);

        scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
    }

    [TestMethod]
    public async Task ProbeEndpointAsync_WhenThresholdBreached_TriggersIncidentAndAlert()
    {
        var ep = MonitoredEndpoint.Create("Failing Gateway", "https://api.fail.com", failureThreshold: 2);
        ep.RecordFailure("Error 1", 500, 100);

        _httpClientMock.Setup(h => h.ExecuteProbeAsync(ep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeExecution.Create(ep.Id, 503, 300, false, "503 Service Unavailable", 2, "corr"));

        _incidentRepoMock.Setup(r => r.GetOpenIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Incident>());

        var worker = new EndpointProbeWorker(_scopeFactoryMock.Object, NullLogger<EndpointProbeWorker>.Instance);

        var result = await worker.ProbeEndpointAsync(ep, _serviceProviderMock.Object, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(EndpointHealthStatus.Unhealthy, ep.Status);
        Assert.AreEqual(2, ep.ConsecutiveFailures);

        _incidentRepoMock.Verify(r => r.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Once);
        _alertDispatcherMock.Verify(d => d.DispatchIncidentAlertAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ProbeEndpointAsync_WhenEndpointRecovers_ResolvesOpenIncidentAndDispatchesResolution()
    {
        var ep = MonitoredEndpoint.Create("Recovering Gateway", "https://api.recover.com", failureThreshold: 2);
        ep.RecordFailure("Fail", 500, 100);
        ep.RecordFailure("Fail", 500, 100);

        var incident = Incident.Trigger(ep.Id, ep.Name, "Failed");

        _httpClientMock.Setup(h => h.ExecuteProbeAsync(ep, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProbeExecution.Create(ep.Id, 200, 42, true, null, 0, "corr"));

        _incidentRepoMock.Setup(r => r.GetOpenIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Incident> { incident });

        var worker = new EndpointProbeWorker(_scopeFactoryMock.Object, NullLogger<EndpointProbeWorker>.Instance);

        var result = await worker.ProbeEndpointAsync(ep, _serviceProviderMock.Object, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EndpointHealthStatus.Healthy, ep.Status);
        Assert.AreEqual(IncidentStatus.Resolved, incident.Status);

        _incidentRepoMock.Verify(r => r.UpdateAsync(incident, It.IsAny<CancellationToken>()), Times.Once);
        _alertDispatcherMock.Verify(d => d.DispatchResolutionAlertAsync(incident, It.IsAny<CancellationToken>()), Times.Once);
    }
}
