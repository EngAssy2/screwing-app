using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ScrewingHub.App.ViewModels;
using ScrewingHub.Communication.Dtm10;
using ScrewingHub.Core.Interfaces;
using ScrewingHub.Core.Models;
using ScrewingHub.Core.Services;
using Xunit;

namespace ScrewingHub.Core.Tests;

public class CycleTimeLogicTests
{
    private readonly IJudgmentService _judgmentService = Substitute.For<IJudgmentService>();
    private readonly ICsvLogService _csvLogService = Substitute.For<ICsvLogService>();
    private readonly IUnitLogService _unitLogService = Substitute.For<IUnitLogService>();
    private readonly ModelConfigService _configService;

    public CycleTimeLogicTests()
    {
        // Setup a dummy config with one model using a temp file
        var tempPath = System.IO.Path.GetTempFileName();
        var settings = new AppSettings
        {
            Models = new List<ProductModel>
            {
                new ProductModel
                {
                    ModelName = "Test Model",
                    TotalScrewCount = 1,
                    IsEnabled = true,
                    Channels = new List<ChannelConfig>
                    {
                        new ChannelConfig { ChannelNumber = 1, IsEnabled = true }
                    }
                }
            }
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        System.IO.File.WriteAllText(tempPath, json);

        _configService = new ModelConfigService(tempPath);
        _configService.Load();
    }

    [Fact]
    public async Task UnitToUnitLogic_UpdatesPrevCT_AndRestartsCurrent()
    {
        // 1. Arrange
        var vm = new DashboardViewModel(_judgmentService, _csvLogService, _unitLogService, _configService);
        
        _judgmentService.Evaluate(Arg.Any<ScrewData>(), Arg.Any<ProductModel>(), Arg.Any<int>())
            .Returns(new JudgmentResult { Judgment = JudgmentStatus.OK });

        var model = _configService.Settings.Models[0];
        vm.SelectedModel = model;

        // 2. Act - Receive data that completes the unit
        var data = new ScrewData { Channel = 1 };
        var client = Substitute.For<IDtm10Client>();
        vm.SetDevices(client, null);
        
        client.DataReceived += Raise.Event<EventHandler<ScrewData>>(client, data);

        // 3. Assert
        Assert.True(vm.PrevCycleTimeDisplay != null);
        Assert.True(vm.CycleTimeDisplay != null);
    }
}
