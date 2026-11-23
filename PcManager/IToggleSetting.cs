namespace PcManager;

public interface IToggleSetting
{
    object Status { get; }

    bool? IsEnabled { get; }

    void Enable();

    void Disable();
}
