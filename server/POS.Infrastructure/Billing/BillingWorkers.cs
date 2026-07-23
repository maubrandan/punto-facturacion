using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces.Billing;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Billing;

public sealed class BillingRenewalWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BillingOptions> _options;
    private readonly ILogger<BillingRenewalWorker> _logger;

    public BillingRenewalWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BillingOptions> options,
        ILogger<BillingRenewalWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.Value;
            if (opts.EnableRenewalJob && !opts.IsNone)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var jobs = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingJobs>();
                    await jobs.ProcessRenewalsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en job de renovación de suscripciones.");
                }
            }

            var delay = Math.Clamp(opts.JobsPollSeconds, 15, 3600);
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
        }
    }
}

public sealed class BillingDunningWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BillingOptions> _options;
    private readonly ILogger<BillingDunningWorker> _logger;

    public BillingDunningWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BillingOptions> options,
        ILogger<BillingDunningWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.Value;
            if (opts.EnableDunningJob && !opts.IsNone)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var jobs = scope.ServiceProvider.GetRequiredService<ISubscriptionBillingJobs>();
                    await jobs.ProcessDunningAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en job de dunning de suscripciones.");
                }
            }

            var delay = Math.Clamp(opts.JobsPollSeconds, 15, 3600);
            await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
        }
    }
}
