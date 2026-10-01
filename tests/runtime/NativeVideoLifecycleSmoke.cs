using Exchanger.Core.Configuration;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using PromotionPlayer = Exchanger.UI.AdvertisementPlayer.AdvertisementPlayer;

namespace Exchanger.Tests.Runtime;

/// <summary>
/// Сквозная проверка штатного lifecycle промо-плеера с Native Video.
/// Реальное оборудование не используется; пути к роликам в журнал не выводятся.
/// </summary>
public partial class NativeVideoLifecycleSmoke : Node
{
    private const int Cycles = 50;
    private const int RecreateEvery = 10;
    private const int HideEvery = 5;

    private PackedScene _playerScene = null!;
    private PromotionPlayer? _host;

    public override async void _Ready()
    {
        try
        {
            string[] paths = OS.GetCmdlineUserArgs()
                .Where(argument => !argument.StartsWith("--", StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (paths.Length < 2)
            {
                Fail("требуются два внешних пути к видео");
                return;
            }

            _playerScene = ResourceLoader.Load<PackedScene>(
                "res://src/ui/AdvertisementPlayer/AdvertisementPlayer.tscn");
            await CreatePlayerAsync();

            for (int cycle = 0; cycle < Cycles; cycle++)
            {
                PromotionPlayer host = _host!;
                host.Configure(new AdvertisementSettings
                {
                    Enabled = true,
                    Muted = false,
                    VideoPaths = [paths[cycle % paths.Length]],
                    FallbackText = "ПРОВЕРКА ПРОМО",
                });
                await WaitFramesAsync(6);

                VideoStreamPlayer player = host.GetNode<VideoStreamPlayer>("VideoPlayer");
                if (player.Stream is null || !player.IsPlaying())
                {
                    Fail($"ролик не запустился в цикле {cycle}");
                    return;
                }

                if ((cycle + 1) % HideEvery == 0)
                {
                    host.Visible = false;
                    await WaitFramesAsync(2);
                    if (player.Stream is not null || player.IsPlaying())
                    {
                        Fail($"скрытый промо-блок не освободил stream в цикле {cycle}");
                        return;
                    }

                    host.Visible = true;
                    await WaitFramesAsync(6);
                    if (player.Stream is null || !player.IsPlaying() || player.StreamPosition > 0.5)
                    {
                        Fail($"промо-блок не перезапустился с начала в цикле {cycle}");
                        return;
                    }
                }

                if ((cycle + 1) % RecreateEvery == 0 && cycle + 1 < Cycles)
                {
                    host.QueueFree();
                    _host = null;
                    await WaitFramesAsync(3);
                    await CreatePlayerAsync();
                }
            }

            _host!.QueueFree();
            _host = null;
            await WaitFramesAsync(4);
            GD.Print($"ADVERTISEMENT_LIFECYCLE: PASS — {Cycles} source changes completed");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            Fail($"необработанная ошибка lifecycle: {exception.GetType().Name}");
        }
    }

    private async Task CreatePlayerAsync()
    {
        _host = _playerScene.Instantiate<PromotionPlayer>();
        AddChild(_host);
        await WaitFramesAsync(2);
    }

    private async Task WaitFramesAsync(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void Fail(string reason)
    {
        GD.PushError("ADVERTISEMENT_LIFECYCLE: FAIL — " + reason);
        if (GodotObject.IsInstanceValid(_host))
        {
            _host!.QueueFree();
        }
        GetTree().Quit(1);
    }
}
