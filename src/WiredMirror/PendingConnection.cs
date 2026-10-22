namespace WiredMirror;

internal sealed record PendingConnection(string Key, VideoProfile Profile, bool Audio, DateTime Expires)
{
    internal static PendingConnection Request(PhoneDevice phone, VideoProfile profile, bool audio, DateTime now)
    {
        if (phone.Connection != ConnectionKind.Usb) throw new InvalidOperationException("Only a chosen USB phone can wait for Trust or setup.");
        return new(phone.Key, profile, audio, now.AddMinutes(2));
    }
    internal bool IsCurrent(string? selected, DateTime now) => selected == Key && now <= Expires;
    internal PhoneDevice? Ready(string? selected, IEnumerable<PhoneDevice> devices, DateTime now) =>
        !IsCurrent(selected, now) ? null : devices.FirstOrDefault(phone => phone.Key == Key && phone.CanConnect && phone.Connection == ConnectionKind.Usb);
}
