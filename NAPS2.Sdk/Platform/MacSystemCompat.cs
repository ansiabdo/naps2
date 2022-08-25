﻿using System.Runtime.InteropServices;
using NAPS2.Platform.Mac;

namespace NAPS2.Platform;

public class MacSystemCompat : ISystemCompat
{
    private const int RTLD_LAZY = 1;
    private const int RTLD_GLOBAL = 8;

    public bool IsWiaDriverSupported => false;

    public bool IsWia20Supported => false;

    public bool IsTwainDriverSupported => true;

    public bool IsSaneDriverSupported => false;

    public bool CanUseWin32 => false;

    public bool UseSystemTesseract => true;

    public bool RenderInWorker => false;

    public string? TesseractExecutablePath => null;

    public string PdfiumLibraryPath
    {
        get
        {
            var archPath = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "_macarm/libpdfium.dylib"
                : "_mac/libpdfium.dylib";
            if (File.Exists(Path.Combine(AssemblyHelper.LibFolder, archPath)))
            {
                return archPath;
            }
            return "libpdfium.dylib";
        }
    }

    public IntPtr LoadLibrary(string path) => MacInterop.dlopen(path, RTLD_LAZY | RTLD_GLOBAL);

    public IntPtr LoadSymbol(IntPtr libraryHandle, string symbol) => MacInterop.dlsym(libraryHandle, symbol);

    public IDisposable FileReadLock(string path) => new FileStream(path, FileMode.Open, FileAccess.Read);

    public IDisposable FileWriteLock(string path) => new FileStream(path, FileMode.Open, FileAccess.Write);
}