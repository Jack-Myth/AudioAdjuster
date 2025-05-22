using Hardcodet.Wpf.TaskbarNotification.Interop;
using Microsoft.VisualBasic.Logging;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AudioAdjuster;

public static class NativeMethods
{
    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    private const uint FILE_READ_EA = 0x0008;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;

    [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string filename,
            [MarshalAs(UnmanagedType.U4)] uint access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] uint flagsAndAttributes,
            IntPtr templateFile);

    public static string GetFinalPathName(string path)
    {
        var h = CreateFile(path,
            FILE_READ_EA,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);
        if (h == INVALID_HANDLE_VALUE)
            throw new Win32Exception();

        try
        {
            var sb = new StringBuilder(1024);
            var res = GetFinalPathNameByHandle(h, sb, 1024, 0);
            if (res == 0)
                throw new Win32Exception();

            return sb.ToString();
        }
        finally
        {
            CloseHandle(h);
        }
    }
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumerator
{
}

internal enum EDataFlow
{
    eRender,
    eCapture,
    eAll,
    EDataFlow_enum_count
}

internal enum ERole
{
    eConsole,
    eMultimedia,
    eCommunications,
    ERole_enum_count
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int NotImpl1();

    [PreserveSig]
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);

    // the rest is not implemented
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

    // the rest is not implemented
}

[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    int NotImpl1();
    int NotImpl2();

    [PreserveSig]
    int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);

    // the rest is not implemented
}

[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    [PreserveSig]
    int GetCount(out int SessionCount);

    [PreserveSig]
    int GetSession(int SessionCount, out IAudioSessionControl Session);
}

[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl
{
    int NotImpl1();

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

    int NotImpl2();
    int NotImpl3();
    int NotImpl4();
    int NotImpl5();
    int NotImpl6();
    int NotImpl7(); 
    int NotImpl8();
}

[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    int NotImpl1();

    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

    int NotImpl2();
    int NotImpl3();
    int NotImpl4();
    int NotImpl5();
    int NotImpl6();
    int NotImpl7();
    int NotImpl8();
    int NotImpl9();
    [PreserveSig]
    int GetSessionIdentifier(
            [Out][MarshalAs(UnmanagedType.LPWStr)] out string retVal);
    [PreserveSig]
    int GetSessionInstanceIdentifier(
            [Out][MarshalAs(UnmanagedType.LPWStr)] out string retVal);

    [PreserveSig]
    int GetProcessId(
            [Out] out UInt32 retVal);
}

[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleAudioVolume
{
    [PreserveSig]
    int SetMasterVolume(float fLevel, ref Guid EventContext);

    [PreserveSig]
    int GetMasterVolume(out float pfLevel);

    [PreserveSig]
    int SetMute(bool bMute, ref Guid EventContext);

    [PreserveSig]
    int GetMute(out bool pbMute);
}

public class AudioHelper
{
    public static float? GetApplicationVolume(int inpid)
    {
        ISimpleAudioVolume? volume = GetVolumeObject(inpid);
        if (volume == null)
            return null;

        float level;
        volume.GetMasterVolume(out level);
        return level * 100;
    }

    public static bool? GetApplicationMute(int inpid)
    {
        ISimpleAudioVolume? volume = GetVolumeObject(inpid);
        if (volume == null)
            return null;

        bool mute;
        volume.GetMute(out mute);
        return mute;
    }

    public static void SetApplicationVolume(int inpid, float level)
    {
        ISimpleAudioVolume? volume = GetVolumeObject(inpid);
        if (volume == null)
            return;

        Guid guid = Guid.Empty;
        volume.SetMasterVolume(level / 100, ref guid);
    }

    public static void SetApplicationMute(int inpid, bool mute)
    {
        ISimpleAudioVolume? volume = GetVolumeObject(inpid);
        if (volume == null)
            return;

        Guid guid = Guid.Empty;
        volume.SetMute(mute, ref guid);
    }

    public static IEnumerable<KeyValuePair<string,int>> EnumerateApplications()
    {
        // get the speakers (1st render + multimedia) device
        IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
        IMMDevice speakers;
        deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);

        // activate the session manager. we need the enumerator
        Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
        object o;
        speakers.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
        IAudioSessionManager2 mgr = (IAudioSessionManager2)o;

        // enumerate sessions for on this device
        IAudioSessionEnumerator sessionEnumerator;
        mgr.GetSessionEnumerator(out sessionEnumerator);
        int count;
        sessionEnumerator.GetCount(out count);

        for (int i = 0; i < count; i++)
        {
            IAudioSessionControl ctl;
            sessionEnumerator.GetSession(i, out ctl);
            string session;
            (ctl as IAudioSessionControl2).GetSessionIdentifier(out session);
            session = session.Substring(session.IndexOf('|') + 1);
            string PIDStr = session.Substring(session.LastIndexOf("b") + 1);
            int PID = 0;
            if(!PIDStr.Contains("#"))
            {
                PID = int.Parse(PIDStr);
            }
            session = session.Substring(0, session.IndexOf("%b"));
            if (session.StartsWith("\\Device\\"))
            {
                session = session.Replace("\\Device\\", "\\\\?\\");
                session = NativeMethods.GetFinalPathName(session);
            }
            yield return new KeyValuePair<string,int>(session,PID);
            Marshal.ReleaseComObject(ctl);
        }
        Marshal.ReleaseComObject(sessionEnumerator);
        Marshal.ReleaseComObject(mgr);
        Marshal.ReleaseComObject(speakers);
        Marshal.ReleaseComObject(deviceEnumerator);
    }

    private static ISimpleAudioVolume? GetVolumeObject(int inpid)
    {
        // get the speakers (1st render + multimedia) device
        IMMDeviceEnumerator deviceEnumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
        IMMDevice speakers;
        deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);

        // activate the session manager. we need the enumerator
        Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
        object o;
        speakers.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out o);
        IAudioSessionManager2 mgr = (IAudioSessionManager2)o;

        // enumerate sessions for on this device
        IAudioSessionEnumerator sessionEnumerator;
        mgr.GetSessionEnumerator(out sessionEnumerator);
        int count;
        sessionEnumerator.GetCount(out count);

        // search for an audio session with the required name
        // NOTE: we could also use the process id instead of the app name (with IAudioSessionControl2)
        ISimpleAudioVolume? volumeControl = null;
        for (int i = 0; i < count; i++)
        {
            IAudioSessionControl ctl;
            sessionEnumerator.GetSession(i, out ctl);
            string session;
            (ctl as IAudioSessionControl2).GetSessionIdentifier(out session);
            string PIDStr = session.Substring(session.LastIndexOf("b") + 1);
            int PID = 0;
            if (!PIDStr.Contains("#"))
            {
                PID = int.Parse(PIDStr);
            }
            if (PID != 0 && PID == inpid)
            {
                volumeControl = ctl as ISimpleAudioVolume;
                break;
            }
            Marshal.ReleaseComObject(ctl);
        }
        Marshal.ReleaseComObject(sessionEnumerator);
        Marshal.ReleaseComObject(mgr);
        Marshal.ReleaseComObject(speakers);
        Marshal.ReleaseComObject(deviceEnumerator);
        return volumeControl;
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 3;

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    private HashSet<KeyValuePair<string,int>> FocusableApps = new HashSet<KeyValuePair<string, int>>();

    private int FocusingAppPid = 0;

    private WinEventDelegate? HoldingDelegate = null;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void DoRefresh()
    {
        this.AppList.Items.Clear();
        var Applications = AudioHelper.EnumerateApplications();
        foreach (var app in Applications)
        {
            CheckBox cb = new CheckBox();
            cb.Content = app;
            cb.DataContext = app;
            cb.IsChecked = FocusableApps.Contains(app);
            cb.Checked += AppItem_CheckStateChanged;
            cb.Unchecked += AppItem_CheckStateChanged;
            this.AppList.Items.Add(cb);
        }
    }

    private void AppItem_CheckStateChanged(object sender, RoutedEventArgs e)
    {
        CheckBox cb = (CheckBox)sender;
        KeyValuePair<string, int> app = (KeyValuePair<string, int>)cb.DataContext;
        if (cb.IsChecked == true)
        {
            FocusableApps.Add(app);
        }
        else
        {
            FocusableApps.Remove(app);
        }
    }

    private void DoApply()
    {
        if(HoldingDelegate==null)
        {
            HoldingDelegate = new WinEventDelegate(WinEventProc);
            IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, HoldingDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }
    }

    public void DoFocus(int FocusAppPID)
    {
        FocusingAppPid = FocusAppPID;
        var Applications = AudioHelper.EnumerateApplications();
        foreach (var app in Applications)
        {
            if (app.Value != FocusingAppPid)
            {
                AudioHelper.SetApplicationMute(app.Value, true);
            }
        }
        AudioHelper.SetApplicationMute(FocusingAppPid, false);
    }

    public void DoUnfocus()
    {
        FocusingAppPid = 0;
        var Applications = AudioHelper.EnumerateApplications();
        foreach (var app in Applications)
        {
            AudioHelper.SetApplicationMute(app.Value, false);
        }
    }

    public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd != IntPtr.Zero)
        {
            HashSet<int> pids = new HashSet<int>();
            foreach(var x in FocusableApps)
            {
                pids.Add(x.Value);
            }
            uint curPID;
            GetWindowThreadProcessId(hwnd, out curPID);
            if (pids.Contains((int)curPID))
            {
                if(curPID != 0 && FocusingAppPid != curPID)
                {
                    DoUnfocus();
                }
                DoFocus((int)curPID);
            }
            else if (FocusingAppPid != 0)
            {
                DoUnfocus();
            }
        }

    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        DoRefresh();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
        DoApply();
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        DoUnfocus();
    }

    private void myNotifyIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e)
    {
        // this.Visibility = Visibility.Visible;
        this.Show();
    }
}