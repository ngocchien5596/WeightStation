using System;
using StationApp.Application.Services;
Console.WriteLine(AppUpdateVersionComparer.Compare("1.0.0 0601", "1.0.0 0101"));
Console.WriteLine(AppUpdateVersionComparer.NormalizeString("1.0.0 0601"));
Console.WriteLine(AppUpdateVersionComparer.NormalizeString("1.0.0 0101"));
