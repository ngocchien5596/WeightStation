using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;

namespace StationApp.UI.Printing;

public sealed class PrintTemplateProvider : IPrintTemplateProvider
{
    private const string ProfilesFileName = "print-template-profiles.json";
    private const string WeighTicketA5V2ProfileKey = "weigh-pc-ver-2-a5-mau-moi";
    private const string WeighTicketA5V2DisplayName = "PC ver 2 - A5 mẫu mới";
    private const string DeliveryTicketA5V2ProfileKey = "delivery-pgn-ver-2-a5-mau-moi";
    private const string DeliveryTicketA5V2DisplayName = "PGN ver 2 - A5 m\u1eabu m\u1edbi";
    private const string OverToleranceInspectionReportProfileKey = "bien-ban-kiem-tra-so-luong-hang-tren-xe-a4";
    private const string OverToleranceInspectionReportDisplayName = "Bi\u00ean b\u1ea3n ki\u1ec3m tra SL h\u00e0ng - A4";
    private const double DeliveryTicketFontSize = 12.5d;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly StationDbContext _dbContext;
    private readonly IAppConfigRepository _appConfigRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IClock _clock;

    public PrintTemplateProvider(
        StationDbContext dbContext,
        IAppConfigRepository appConfigRepository,
        ICurrentUserContext currentUserContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _appConfigRepository = appConfigRepository;
        _currentUserContext = currentUserContext;
        _clock = clock;
    }

    public Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, CancellationToken ct)
        => GetTemplateAsync(kind, null, ct);

    public async Task<PrintTemplateDefinition> GetTemplateAsync(PrintDocumentKind kind, string? profileKey, CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var profile = ResolveProfile(store, kind, profileKey);
        var fields = GetDefaultFields(kind, profile);
        var supportsDotMatrixTextMode = kind == PrintDocumentKind.DeliveryTicket
            || kind == PrintDocumentKind.OverToleranceInspectionReport
            || IsWeighTicketA5V2Profile(profile);
        return kind switch
        {
            PrintDocumentKind.WeighTicket => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "WeighTicketPrintTemplate",
                PageWidthMm = 210d,
                PageHeightMm = 148.5d,
                DefaultOffsetXmm = profile.OffsetXmm,
                DefaultOffsetYmm = profile.OffsetYmm,
                ActiveProfileKey = profile.ProfileKey,
                ActiveProfileName = profile.DisplayName,
                SupportsDotMatrixTextMode = supportsDotMatrixTextMode,
                Fields = ApplyProfileLayout(fields, profile)
            },
            PrintDocumentKind.DeliveryTicket => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "DeliveryTicketPrintTemplate",
                PageWidthMm = 210d,
                PageHeightMm = IsDeliveryTicketA5V2Profile(profile) ? 148.5d : 297d,
                DefaultOffsetXmm = profile.OffsetXmm,
                DefaultOffsetYmm = profile.OffsetYmm,
                ActiveProfileKey = profile.ProfileKey,
                ActiveProfileName = profile.DisplayName,
                SupportsDotMatrixTextMode = supportsDotMatrixTextMode,
                Fields = ApplyProfileLayout(fields, profile)
            },
            PrintDocumentKind.OverToleranceInspectionReport => new PrintTemplateDefinition
            {
                Kind = kind,
                TemplateName = "OverToleranceInspectionReportPrintTemplate",
                PageWidthMm = 210d,
                PageHeightMm = 297d,
                DefaultOffsetXmm = profile.OffsetXmm,
                DefaultOffsetYmm = profile.OffsetYmm,
                ActiveProfileKey = profile.ProfileKey,
                ActiveProfileName = profile.DisplayName,
                SupportsDotMatrixTextMode = supportsDotMatrixTextMode,
                Fields = ApplyProfileLayout(fields, profile)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public async Task<IReadOnlyList<PrintTemplateProfileDescriptor>> GetProfilesAsync(PrintDocumentKind kind, CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var defaultKey = GetDefaultProfileKey(store, kind);
        return GetProfiles(store, kind)
            .Select(x => new PrintTemplateProfileDescriptor(x.ProfileKey, x.DisplayName, string.Equals(x.ProfileKey, defaultKey, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task SaveLayoutAsync(
        PrintDocumentKind kind,
        string? profileKey,
        double offsetXmm,
        double offsetYmm,
        IReadOnlyList<PrintFieldPosition> fieldPositions,
        CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var profile = ResolveProfile(store, kind, profileKey);
        profile.OffsetXmm = offsetXmm;
        profile.OffsetYmm = offsetYmm;
        profile.TemplateVersion = GetCurrentTemplateVersion(kind);
        profile.Fields = fieldPositions
            .Select(x => new PersistedPrintFieldPosition
            {
                FieldKey = x.FieldKey,
                X = x.X,
                Y = x.Y,
                Width = x.Width,
                IsEnabled = x.IsEnabled
            })
            .ToList();

        await SaveStoreAsync(store, ct);
    }

    public async Task<PrintTemplateProfileDescriptor> CreateProfileAsync(
        PrintDocumentKind kind,
        string displayName,
        double offsetXmm,
        double offsetYmm,
        IReadOnlyList<PrintFieldPosition> fieldPositions,
        CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var profiles = GetProfiles(store, kind);
        var profile = new PersistedPrintTemplateProfile
        {
            ProfileKey = BuildProfileKey(kind, displayName, profiles.Select(x => x.ProfileKey)),
            DisplayName = displayName.Trim(),
            OffsetXmm = offsetXmm,
            OffsetYmm = offsetYmm,
            TemplateVersion = GetCurrentTemplateVersion(kind),
            Fields = fieldPositions.Select(x => new PersistedPrintFieldPosition
            {
                FieldKey = x.FieldKey,
                X = x.X,
                Y = x.Y,
                Width = x.Width,
                IsEnabled = x.IsEnabled
            }).ToList()
        };

        profiles.Add(profile);
        await SaveStoreAsync(store, ct);
        return new PrintTemplateProfileDescriptor(profile.ProfileKey, profile.DisplayName, false);
    }

    public async Task SetDefaultProfileAsync(PrintDocumentKind kind, string profileKey, CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        _ = ResolveProfile(store, kind, profileKey);
        store.DefaultProfileKeys[GetTemplatePrefix(kind)] = profileKey;
        await SaveStoreAsync(store, ct);
    }

    public async Task<string> ExportBackupAsync(CancellationToken ct)
    {
        var store = await LoadStoreAsync(ct);
        var path = GetBackupFilePath();
        var json = JsonSerializer.Serialize(store, JsonOptions);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(path, json, ct);
        return path;
    }

    private static string GetGlobalOffsetKey(PrintDocumentKind kind, bool isX)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => isX ? "print_weigh_offset_x_mm" : "print_weigh_offset_y_mm",
            PrintDocumentKind.DeliveryTicket => isX ? "print_delivery_offset_x_mm" : "print_delivery_offset_y_mm",
            PrintDocumentKind.OverToleranceInspectionReport => isX ? "print_over_tolerance_report_offset_x_mm" : "print_over_tolerance_report_offset_y_mm",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetFieldOffsetKey(PrintDocumentKind kind, string fieldKey, bool isX)
        => $"print_{GetTemplatePrefix(kind)}_field_{fieldKey}_{(isX ? "x" : "y")}_mm";

    private static string GetFieldWidthKey(PrintDocumentKind kind, string fieldKey)
        => $"print_{GetTemplatePrefix(kind)}_field_{fieldKey}_width_mm";

    private static string GetTemplatePrefix(PrintDocumentKind kind)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => "weigh",
            PrintDocumentKind.DeliveryTicket => "delivery",
            PrintDocumentKind.OverToleranceInspectionReport => "over_tolerance_report",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetTemplateVersionKey(PrintDocumentKind kind)
        => $"print_{GetTemplatePrefix(kind)}_layout_version";

    private static int GetCurrentTemplateVersion(PrintDocumentKind kind)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => 2,
            PrintDocumentKind.DeliveryTicket => 3,
            PrintDocumentKind.OverToleranceInspectionReport => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetDefaultProfileStorageKey(PrintDocumentKind kind)
        => GetTemplatePrefix(kind);

    private async Task<PersistedPrintTemplateStore> LoadStoreAsync(CancellationToken ct)
    {
        var store = await LoadStoreFromDatabaseAsync(ct);
        var changed = false;

        if (IsStoreEmpty(store))
        {
            var importedStore = await TryLoadLegacyFileStoreAsync(ct);
            if (importedStore != null)
            {
                store = importedStore;
                changed = true;
            }
        }

        changed |= await EnsureSeedProfileAsync(store, PrintDocumentKind.WeighTicket, "PC ver 1", WeighTicketFields, ct);
        changed |= EnsureAdditionalSeedProfile(
            store,
            PrintDocumentKind.WeighTicket,
            WeighTicketA5V2ProfileKey,
            WeighTicketA5V2DisplayName,
            WeighTicketA5V2Fields);
        changed |= await EnsureSeedProfileAsync(store, PrintDocumentKind.DeliveryTicket, "PGN ver 1", DeliveryTicketFields, ct);
        changed |= EnsureAdditionalSeedProfile(
            store,
            PrintDocumentKind.DeliveryTicket,
            DeliveryTicketA5V2ProfileKey,
            DeliveryTicketA5V2DisplayName,
            DeliveryTicketA5V2Fields,
            replaceWhenOlder: true);
        changed |= EnsureAdditionalSeedProfile(
            store,
            PrintDocumentKind.OverToleranceInspectionReport,
            OverToleranceInspectionReportProfileKey,
            OverToleranceInspectionReportDisplayName,
            OverToleranceInspectionReportFields,
            replaceWhenOlder: true);
        if (!store.DefaultProfileKeys.ContainsKey(GetDefaultProfileStorageKey(PrintDocumentKind.OverToleranceInspectionReport)))
        {
            store.DefaultProfileKeys[GetDefaultProfileStorageKey(PrintDocumentKind.OverToleranceInspectionReport)] = OverToleranceInspectionReportProfileKey;
            changed = true;
        }

        if (changed)
        {
            await SaveStoreAsync(store, ct);
        }

        return store;
    }

    private async Task<PersistedPrintTemplateStore> LoadStoreFromDatabaseAsync(CancellationToken ct)
    {
        var store = new PersistedPrintTemplateStore();
        var rows = await _dbContext.PrintTemplateProfiles
            .AsNoTracking()
            .OrderBy(x => x.TemplateKind)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            if (!store.ProfilesByKind.TryGetValue(row.TemplateKind, out var profiles))
            {
                profiles = [];
                store.ProfilesByKind[row.TemplateKind] = profiles;
            }

            profiles.Add(new PersistedPrintTemplateProfile
            {
                ProfileKey = row.ProfileKey,
                DisplayName = row.DisplayName,
                OffsetXmm = row.OffsetXmm,
                OffsetYmm = row.OffsetYmm,
                TemplateVersion = row.TemplateVersion,
                Fields = DeserializeFieldPositions(row.LayoutJson)
            });

            if (row.IsDefault)
            {
                store.DefaultProfileKeys[row.TemplateKind] = row.ProfileKey;
            }
        }

        return store;
    }

    private async Task<PersistedPrintTemplateStore?> TryLoadLegacyFileStoreAsync(CancellationToken ct)
    {
        var storePath = GetProfilesFilePath();
        if (!File.Exists(storePath))
        {
            return null;
        }

        var raw = await File.ReadAllTextAsync(storePath, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PersistedPrintTemplateStore>(raw, JsonOptions);
    }

    private async Task<bool> EnsureSeedProfileAsync(
        PersistedPrintTemplateStore store,
        PrintDocumentKind kind,
        string displayName,
        IReadOnlyList<PrintFieldDefinition> defaults,
        CancellationToken ct)
    {
        var profiles = GetProfiles(store, kind);
        if (profiles.Count > 0)
        {
            if (!store.DefaultProfileKeys.ContainsKey(GetDefaultProfileStorageKey(kind)))
            {
                store.DefaultProfileKeys[GetDefaultProfileStorageKey(kind)] = profiles[0].ProfileKey;
                return true;
            }

            return false;
        }

        var migrated = await MigrateLegacyProfileAsync(kind, displayName, defaults, ct);
        profiles.Add(migrated);
        store.DefaultProfileKeys[GetDefaultProfileStorageKey(kind)] = migrated.ProfileKey;
        return true;
    }

    private static bool EnsureAdditionalSeedProfile(
        PersistedPrintTemplateStore store,
        PrintDocumentKind kind,
        string profileKey,
        string displayName,
        IReadOnlyList<PrintFieldDefinition> defaults,
        bool replaceWhenOlder = false)
    {
        var profiles = GetProfiles(store, kind);
        var existing = profiles.FirstOrDefault(x => string.Equals(x.ProfileKey, profileKey, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (replaceWhenOlder && existing.TemplateVersion < GetCurrentTemplateVersion(kind))
            {
                existing.DisplayName = displayName;
                existing.TemplateVersion = GetCurrentTemplateVersion(kind);
                existing.Fields = defaults.Select(field => new PersistedPrintFieldPosition
                {
                    FieldKey = field.FieldKey,
                    X = field.X,
                    Y = field.Y,
                    Width = field.Width,
                    IsEnabled = field.IsEnabled
                }).ToList();

                return true;
            }

            return false;
        }

        profiles.Add(new PersistedPrintTemplateProfile
        {
            ProfileKey = profileKey,
            DisplayName = displayName,
            OffsetXmm = 0d,
            OffsetYmm = 0d,
            TemplateVersion = GetCurrentTemplateVersion(kind),
            Fields = defaults.Select(field => new PersistedPrintFieldPosition
            {
                FieldKey = field.FieldKey,
                X = field.X,
                Y = field.Y,
                Width = field.Width,
                IsEnabled = field.IsEnabled
            }).ToList()
        });

        return true;
    }

    private async Task<PersistedPrintTemplateProfile> MigrateLegacyProfileAsync(
        PrintDocumentKind kind,
        string displayName,
        IReadOnlyList<PrintFieldDefinition> defaults,
        CancellationToken ct)
    {
        var usePersistedLayout = await ShouldUsePersistedLayoutAsync(kind, ct);
        var positions = new List<PersistedPrintFieldPosition>(defaults.Count);

        foreach (var field in defaults)
        {
            var x = field.X;
            var y = field.Y;
            var width = field.Width;

            if (usePersistedLayout)
            {
                var xRaw = await _appConfigRepository.GetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: true), ct);
                var yRaw = await _appConfigRepository.GetValueAsync(GetFieldOffsetKey(kind, field.FieldKey, isX: false), ct);
                var widthRaw = await _appConfigRepository.GetValueAsync(GetFieldWidthKey(kind, field.FieldKey), ct);
                x = double.TryParse(xRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedX) ? parsedX : x;
                y = double.TryParse(yRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedY) ? parsedY : y;
                width = double.TryParse(widthRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWidth) ? parsedWidth : width;
            }

            positions.Add(new PersistedPrintFieldPosition
            {
                FieldKey = field.FieldKey,
                X = x,
                Y = y,
                Width = width,
                IsEnabled = field.IsEnabled
            });
        }

        return new PersistedPrintTemplateProfile
        {
            ProfileKey = BuildProfileKey(kind, displayName, Array.Empty<string>()),
            DisplayName = displayName,
            OffsetXmm = usePersistedLayout ? await GetLegacyOffsetAsync(kind, true, ct) : 0d,
            OffsetYmm = usePersistedLayout ? await GetLegacyOffsetAsync(kind, false, ct) : 0d,
            TemplateVersion = GetCurrentTemplateVersion(kind),
            Fields = positions
        };
    }

    private async Task<double> GetLegacyOffsetAsync(PrintDocumentKind kind, bool isX, CancellationToken ct)
    {
        var raw = await _appConfigRepository.GetValueAsync(GetGlobalOffsetKey(kind, isX), ct);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }

    private async Task<bool> ShouldUsePersistedLayoutAsync(PrintDocumentKind kind, CancellationToken ct)
    {
        var raw = await _appConfigRepository.GetValueAsync(GetTemplateVersionKey(kind), ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var savedVersion)
               && savedVersion >= GetCurrentTemplateVersion(kind);
    }

    private static IReadOnlyList<PrintFieldDefinition> ApplyProfileLayout(
        IReadOnlyList<PrintFieldDefinition> defaults,
        PersistedPrintTemplateProfile profile)
    {
        var positions = profile.Fields.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);
        return defaults
            .Select(field => positions.TryGetValue(field.FieldKey, out var pos)
                ? field with
                {
                    X = pos.X,
                    Y = pos.Y,
                    Width = pos.Width ?? field.Width,
                    IsEnabled = pos.IsEnabled
                }
                : field)
            .ToList();
    }

    private static IReadOnlyList<PrintFieldDefinition> GetDefaultFields(PrintDocumentKind kind, PersistedPrintTemplateProfile profile)
        => kind switch
        {
            PrintDocumentKind.WeighTicket => IsWeighTicketA5V2Profile(profile) ? WeighTicketA5V2Fields : WeighTicketFields,
            PrintDocumentKind.DeliveryTicket => IsDeliveryTicketA5V2Profile(profile) ? DeliveryTicketA5V2Fields : DeliveryTicketFields,
            PrintDocumentKind.OverToleranceInspectionReport => OverToleranceInspectionReportFields,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static bool IsWeighTicketA5V2Profile(PersistedPrintTemplateProfile profile)
        => string.Equals(profile.ProfileKey, WeighTicketA5V2ProfileKey, StringComparison.OrdinalIgnoreCase)
           || profile.Fields.Any(x =>
               string.Equals(x.FieldKey, "TransactionTypeDisplayShort", StringComparison.OrdinalIgnoreCase)
               || string.Equals(x.FieldKey, "CutOrderCode", StringComparison.OrdinalIgnoreCase)
               || string.Equals(x.FieldKey, "NetWeightKg", StringComparison.OrdinalIgnoreCase));

    private static bool IsDeliveryTicketA5V2Profile(PersistedPrintTemplateProfile profile)
        => string.Equals(profile.ProfileKey, DeliveryTicketA5V2ProfileKey, StringComparison.OrdinalIgnoreCase)
           || profile.Fields.Any(x =>
               string.Equals(x.FieldKey, "PackagePrinterName", StringComparison.OrdinalIgnoreCase)
               || string.Equals(x.FieldKey, "ReceiverName", StringComparison.OrdinalIgnoreCase));

    private static PersistedPrintTemplateProfile ResolveProfile(PersistedPrintTemplateStore store, PrintDocumentKind kind, string? profileKey)
    {
        var profiles = GetProfiles(store, kind);
        var effectiveKey = string.IsNullOrWhiteSpace(profileKey) ? GetDefaultProfileKey(store, kind) : profileKey;
        return profiles.FirstOrDefault(x => string.Equals(x.ProfileKey, effectiveKey, StringComparison.OrdinalIgnoreCase))
            ?? profiles.First();
    }

    private static List<PersistedPrintTemplateProfile> GetProfiles(PersistedPrintTemplateStore store, PrintDocumentKind kind)
    {
        var storageKey = GetDefaultProfileStorageKey(kind);
        if (!store.ProfilesByKind.TryGetValue(storageKey, out var profiles))
        {
            profiles = [];
            store.ProfilesByKind[storageKey] = profiles;
        }

        return profiles;
    }

    private static string GetDefaultProfileKey(PersistedPrintTemplateStore store, PrintDocumentKind kind)
    {
        var storageKey = GetDefaultProfileStorageKey(kind);
        return store.DefaultProfileKeys.TryGetValue(storageKey, out var key) && !string.IsNullOrWhiteSpace(key)
            ? key
            : GetProfiles(store, kind).First().ProfileKey;
    }

    private async Task SaveStoreAsync(PersistedPrintTemplateStore store, CancellationToken ct)
    {
        var existing = await _dbContext.PrintTemplateProfiles.ToListAsync(ct);
        if (existing.Count > 0)
        {
            _dbContext.PrintTemplateProfiles.RemoveRange(existing);
        }

        var actor = string.IsNullOrWhiteSpace(_currentUserContext.Username) ? "SYSTEM" : _currentUserContext.Username;
        var now = _clock.NowLocal;
        var rows = new List<PrintTemplateProfile>();

        foreach (var (kindKey, profiles) in store.ProfilesByKind)
        {
            var defaultKey = store.DefaultProfileKeys.TryGetValue(kindKey, out var key) ? key : profiles.FirstOrDefault()?.ProfileKey;
            rows.AddRange(profiles.Select(profile => new PrintTemplateProfile
            {
                Id = Guid.NewGuid(),
                TemplateKind = kindKey,
                ProfileKey = profile.ProfileKey,
                DisplayName = profile.DisplayName,
                IsDefault = string.Equals(profile.ProfileKey, defaultKey, StringComparison.OrdinalIgnoreCase),
                OffsetXmm = profile.OffsetXmm,
                OffsetYmm = profile.OffsetYmm,
                TemplateVersion = profile.TemplateVersion,
                LayoutJson = JsonSerializer.Serialize(profile.Fields, JsonOptions),
                CreatedAt = now,
                CreatedBy = actor,
                UpdatedAt = now,
                UpdatedBy = actor
            }));
        }

        if (rows.Count > 0)
        {
            await _dbContext.PrintTemplateProfiles.AddRangeAsync(rows, ct);
        }

        await _dbContext.SaveChangesAsync(ct);
        await ExportBackupAsync(ct);
    }

    private static bool IsStoreEmpty(PersistedPrintTemplateStore store)
        => store.ProfilesByKind.Values.All(x => x.Count == 0);

    private static List<PersistedPrintFieldPosition> DeserializeFieldPositions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<PersistedPrintFieldPosition>>(json, JsonOptions) ?? [];
    }

    private static string BuildProfileKey(PrintDocumentKind kind, string displayName, IEnumerable<string> existingKeys)
    {
        var prefix = GetTemplatePrefix(kind);
        var baseKey = string.Concat(displayName
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'))
            .Trim('-');
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            baseKey = $"{prefix}-ver";
        }

        var key = $"{prefix}-{baseKey}";
        var set = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        if (!set.Contains(key))
        {
            return key;
        }

        var index = 2;
        while (set.Contains($"{key}-{index}"))
        {
            index++;
        }

        return $"{key}-{index}";
    }

    private static string GetBackupFilePath()
        => Path.Combine(AppContext.BaseDirectory, "print-layout-backup.json");

    private static string GetProfilesFilePath()
        => Path.Combine(AppContext.BaseDirectory, ProfilesFileName);

    private sealed class PersistedPrintTemplateStore
    {
        public Dictionary<string, List<PersistedPrintTemplateProfile>> ProfilesByKind { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DefaultProfileKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PersistedPrintTemplateProfile
    {
        public string ProfileKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public double OffsetXmm { get; set; }
        public double OffsetYmm { get; set; }
        public int TemplateVersion { get; set; }
        public List<PersistedPrintFieldPosition> Fields { get; set; } = [];
    }

    private sealed class PersistedPrintFieldPosition
    {
        public string FieldKey { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double? Width { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    private static readonly IReadOnlyList<PrintFieldDefinition> WeighTicketFields =
    [
        new("StaticCompanyLogo", 32, 7.5, 12, PrintFieldAlignment.Left, 7, PrintFieldWeight.Normal, IsImage: true, ImageSourceUri: "pack://application:,,,/Assets/logo.jpg"),
        new("StaticCompanyName", 108, 8, 58, PrintFieldAlignment.Center, 8.8, PrintFieldWeight.Bold, LiteralValue: "C\u00d4NG TY C\u1ed4 PH\u1ea6N XI M\u0102NG C\u1ea8M PH\u1ea2", Underline: true),
        new("StaticCompanyAddress", 104, 16, 68, PrintFieldAlignment.Center, 7.1, PrintFieldWeight.Normal, LiteralValue: "\u0110C: Km6, Qu\u1ed1c l\u1ed9 18A, Ph\u01b0\u1eddng Quang Hanh, T\u1ec9nh Qu\u1ea3ng Ninh", Italic: true),
        new("StaticCompanyPhone", 107, 22, 62, PrintFieldAlignment.Center, 7.1, PrintFieldWeight.Normal, LiteralValue: "\u0110T: (020)33.721.995  -  (020)33.721.996", Italic: true),
        new("StaticTitle", 164, 7, 32, PrintFieldAlignment.Center, 16.5, PrintFieldWeight.Bold, LiteralValue: "PHI\u1ebeU C\u00c2N", Underline: true),
        new("StaticTicketLabel", 149, 22, 18, PrintFieldAlignment.Right, 9.2, PrintFieldWeight.Bold, LiteralValue: "S\u1ed1 phi\u1ebfu:", ShadedBackground: true),
        new("TicketNo", 168, 22, 22, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Bold, ShadedBackground: true),
        new("StaticVehicleSectionLine", 14, 28, 182, PrintFieldAlignment.Left, 8.5, PrintFieldWeight.Normal, IsLine: true),

        new("StaticVehicleRegistrationLabel", 32, 32, 16, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Tem xe:"),
        new("VehicleRegistrationNo", 52, 32, 60, PrintFieldAlignment.Left, 10.4, PrintFieldWeight.Bold),
        new("StaticWeight1Label", 164, 32, 13, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Gi\u1edd v\u00e0o:"),
        new("Weight1DateTime", 182, 32, 29, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Normal),

        new("StaticVehiclePlateLabel", 32, 42, 18, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Bi\u1ec3n s\u1ed1 xe:"),
        new("VehiclePlate", 52, 42, 78, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Bold),
        new("StaticWeight2Label", 164, 42, 11, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Gi\u1edd ra:"),
        new("Weight2DateTime", 182, 42, 29, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Normal),

        new("StaticMoocRegistrationLabel", 32, 52, 18, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Tem mo\u00f3c:"),
        new("MoocRegistrationNo", 52, 52, 60, PrintFieldAlignment.Left, 10.4, PrintFieldWeight.Bold),
        new("StaticGrossWeightLabel", 157, 52, 24, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Tr\u1ecdng l\u01b0\u1ee3ng t\u1ed5ng (t\u1ea5n):"),
        new("GrossWeight", 199, 52, 12, PrintFieldAlignment.Left, 11.8, PrintFieldWeight.Bold),

        new("StaticProductLabel", 32, 62, 16, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "H\u00e0ng h\u00f3a:"),
        new("ProductName", 52, 62, 82, PrintFieldAlignment.Left, 10.6, PrintFieldWeight.Normal, 3, PrintWrapMode.Wrap),
        new("StaticEmptyWeightLabel", 157, 62, 22, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Tr\u1ecdng l\u01b0\u1ee3ng xe (t\u1ea5n):"),
        new("EmptyWeight", 199, 62, 12, PrintFieldAlignment.Left, 11.8, PrintFieldWeight.Bold),

        new("StaticLotNoLabel", 32, 75, 14, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "L\u00f4 h\u00e0ng:"),
        new("LotNo", 52, 75, 40, PrintFieldAlignment.Left, 10.2, PrintFieldWeight.Normal),
        new("StaticNetWeightLabel", 151, 75, 28, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Tr\u1ecdng l\u01b0\u1ee3ng h\u00e0ng (t\u1ea5n):"),
        new("NetWeight", 199, 75, 12, PrintFieldAlignment.Left, 11.8, PrintFieldWeight.Bold),

        new("StaticCustomerLabel", 32, 85, 18, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "Kh\u00e1ch h\u00e0ng:"),
        new("CustomerName", 52, 85, 82, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap),
        new("StaticNotesLabel", 170, 85, 10, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Ghi ch\u00fa:"),
        new("Notes", 182, 85, 30, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Normal, 4, PrintWrapMode.Wrap),

        new("StaticRepresentativeLabel", 32, 101, 16, PrintFieldAlignment.Left, 9.6, PrintFieldWeight.Bold, LiteralValue: "\u0110\u1ea1i di\u1ec7n:"),
        new("RepresentativeName", 52, 101, 68, PrintFieldAlignment.Left, 10.2, PrintFieldWeight.Normal),
        new("StaticPrintedAtLabel", 161, 101, 18, PrintFieldAlignment.Right, 9.4, PrintFieldWeight.Normal, LiteralValue: "Ng\u00e0y in phi\u1ebfu:", Italic: true),
        new("PrintedAt", 182, 101, 29, PrintFieldAlignment.Left, 9.8, PrintFieldWeight.Normal, Italic: true),

        new("StaticSignerLine", 30, 113, 182, PrintFieldAlignment.Left, 8.5, PrintFieldWeight.Normal, IsLine: true),
        new("StaticSigner1", 39, 119, 34, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "\u0110\u1ea1i di\u1ec7n giao nh\u1eadn"),
        new("StaticSigner2", 93, 119, 18, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "L\u00e1i xe"),
        new("StaticSigner3", 135, 119, 20, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "Ki\u1ec3m so\u00e1t"),
        new("StaticSigner4", 175, 119, 24, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, LiteralValue: "Nh\u00e2n vi\u00ean c\u00e2n"),
        new("PrintedBy", 172, 138, 30, PrintFieldAlignment.Center, 9.8, PrintFieldWeight.Bold, Italic: true),
        new("StaticPrintedByUnderline", 172, 144, 30, PrintFieldAlignment.Left, 8.5, PrintFieldWeight.Normal, IsLine: true),

        new("StaticFooterLeft", 20, 132, 32, PrintFieldAlignment.Left, 7.2, PrintFieldWeight.Bold, LiteralValue: "XMCP c\u00e2n 120 t\u1ea5n - C2"),
        new("StaticFooterRight", 70, 141, 100, PrintFieldAlignment.Center, 7.2, PrintFieldWeight.Normal, LiteralValue: "Copyright (2026) by CAMPHACEMENT - www.camphacement.vn")
    ];

    private static readonly IReadOnlyList<PrintFieldDefinition> WeighTicketA5V2Fields =
    [
        new("VehiclePlate", 30, 39, 56, PrintFieldAlignment.Left, 11.5, PrintFieldWeight.Bold),
        new("TicketNo", 154, 39, 34, PrintFieldAlignment.Left, 11.2, PrintFieldWeight.Bold),

        new("VehicleRegistrationNo", 30, 51, 52, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal),
        new("Weight1DateTime", 154, 51, 40, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal),

        new("MoocRegistrationNo", 30, 63, 52, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal),
        new("CustomerName", 30, 75, 78, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap),
        new("TransactionTypeDisplayShort", 30, 90, 28, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Bold),
        new("Weight2DateTime", 154, 75, 40, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal),

        new("Weight1", 54, 96, 30, PrintFieldAlignment.Left, 11.2, PrintFieldWeight.Bold),
        new("BagCount", 154, 96, 25, PrintFieldAlignment.Left, 11.2, PrintFieldWeight.Bold),
        new("Weight2", 54, 108, 30, PrintFieldAlignment.Left, 11.2, PrintFieldWeight.Bold),
        new("CutOrderCode", 154, 108, 44, PrintFieldAlignment.Left, 10.2, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap),
        new("NetWeightKg", 54, 120, 30, PrintFieldAlignment.Left, 11.2, PrintFieldWeight.Bold),
        new("LotNo", 154, 120, 32, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal),

        new("ProductName", 30, 132, 86, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("PrintedBy", 158, 132, 36, PrintFieldAlignment.Center, 10.5, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap)
    ];

    private static readonly IReadOnlyList<PrintFieldDefinition> DeliveryTicketFields =
    [
        new("DeliveryNo", 150, 39, 34, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("ReferenceCode", 150, 54, 34, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("CustomerName", 27, 86, 156, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Market", 132, 104, 49, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("ConsumptionPlace", 27, 104, 103, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("LoadingPlace", 27, 122, 103, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("CustomerCode", 147, 122, 34, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("ProductName", 25, 149, 42, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal, 4, PrintWrapMode.Wrap),
        new("PlannedWeight", 79, 149, 14, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("BagCount", 95, 149, 14, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("ActualWeight", 118, 149, 14, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("ActualBagCount", 134, 149, 14, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("LotNo", 155, 149, 17, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("VehicleLine", 176, 146, 18, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("SealNo", 31, 214, 56, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Weight1Hour", 121, 233, 8, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Weight1Minute", 142, 233, 8, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Weight1Date", 161, 233, 24, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Weight2Hour", 121, 249, 8, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Weight2Minute", 142, 249, 8, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Weight2Date", 161, 249, 24, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal),
        new("Notes", 18, 267, 166, PrintFieldAlignment.Left, DeliveryTicketFontSize, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("PrintedBy", 24, 287, 46, PrintFieldAlignment.Center, DeliveryTicketFontSize, PrintFieldWeight.Normal)
    ];

    private static readonly IReadOnlyList<PrintFieldDefinition> DeliveryTicketA5V2Fields =
    [
        new("DeliveryNo", 158, 30, 34, PrintFieldAlignment.Left, 11.4, PrintFieldWeight.Bold),
        new("ReferenceCode", 158, 39, 40, PrintFieldAlignment.Left, 11.2, PrintFieldWeight.Bold),

        new("CustomerName", 35, 58, 102, PrintFieldAlignment.Left, 11.2, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap),
        new("CustomerCode", 158, 58, 36, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Bold),
        new("ConsumptionPlace", 35, 69, 102, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("LoadingPlace", 158, 69, 42, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap),

        new("ProductName", 25, 90, 45, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, 3, PrintWrapMode.Wrap),
        new("BagCount", 76, 90, 18, PrintFieldAlignment.Center, 10.8, PrintFieldWeight.Normal),
        new("PlannedWeight", 96, 90, 18, PrintFieldAlignment.Center, 10.8, PrintFieldWeight.Normal),
        new("ActualBagCount", 118, 90, 18, PrintFieldAlignment.Center, 10.8, PrintFieldWeight.Normal),
        new("ActualWeight", 139, 90, 18, PrintFieldAlignment.Center, 10.8, PrintFieldWeight.Normal),
        new("VehicleLine", 164, 90, 37, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),

        new("SealNo", 35, 113, 60, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Normal),
        new("LotNo", 158, 113, 38, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Normal),
        new("PackagePrinterName", 35, 123, 98, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap),
        new("Notes", 158, 123, 42, PrintFieldAlignment.Left, 10.8, PrintFieldWeight.Normal, 7, PrintWrapMode.Wrap),

        new("Weight1Hour", 86, 31, 8, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight1Minute", 104, 31, 8, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight1Day", 124, 31, 7, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight1Month", 135, 31, 7, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight1Year", 146, 31, 14, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight2Hour", 86, 39, 8, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight2Minute", 104, 39, 8, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight2Day", 124, 39, 7, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight2Month", 135, 39, 7, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("Weight2Year", 146, 39, 14, PrintFieldAlignment.Center, 10.4, PrintFieldWeight.Normal),
        new("PrintedBy", 33, 137, 48, PrintFieldAlignment.Center, 10.8, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap)
    ];

    private static readonly IReadOnlyList<PrintFieldDefinition> OverToleranceInspectionReportFields =
    [
        new("StaticCompanyName", 18, 12, 174, PrintFieldAlignment.Center, 11.5, PrintFieldWeight.Bold, LiteralValue: "C\u00d4NG TY C\u1ed4 PH\u1ea6N XI M\u0102NG C\u1ea8M PH\u1ea2"),
        new("StaticCompanyAddress", 18, 20, 174, PrintFieldAlignment.Center, 9.5, PrintFieldWeight.Normal, LiteralValue: "\u0110\u1ecba ch\u1ec9: Km6, Qu\u1ed1c l\u1ed9 18A, Ph\u01b0\u1eddng Quang Hanh, T\u1ec9nh Qu\u1ea3ng Ninh"),
        new("StaticCompanyPhone", 18, 27, 174, PrintFieldAlignment.Center, 9.5, PrintFieldWeight.Normal, LiteralValue: "\u0110i\u1ec7n tho\u1ea1i: 0203 721996 - Fax: 0203 714605"),

        new("StaticTitle", 18, 43, 174, PrintFieldAlignment.Center, 16, PrintFieldWeight.Bold, LiteralValue: "BI\u00caN B\u1ea2N X\u00c1C NH\u1eacN"),
        new("StaticTimeLine", 18, 57, 174, PrintFieldAlignment.Center, 11, PrintFieldWeight.Normal, LiteralValue: "H\u00f4m nay v\u00e0o h\u1ed3i .... gi\u1edd .... ph\u00fat, ng\u00e0y .... th\u00e1ng .... n\u0103m ...."),
        new("PrintHour", 75, 57, 9, PrintFieldAlignment.Center, 11, PrintFieldWeight.Bold),
        new("PrintMinute", 96, 57, 9, PrintFieldAlignment.Center, 11, PrintFieldWeight.Bold),
        new("PrintDay", 119, 57, 9, PrintFieldAlignment.Center, 11, PrintFieldWeight.Bold),
        new("PrintMonth", 138, 57, 9, PrintFieldAlignment.Center, 11, PrintFieldWeight.Bold),
        new("PrintYear", 157, 57, 15, PrintFieldAlignment.Center, 11, PrintFieldWeight.Bold),
        new("StaticLocationLine", 18, 66, 174, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "T\u1ea1i Tr\u1ea1m c\u00e2n Nh\u00e0 m\u00e1y Xi m\u0103ng C\u1ea9m Ph\u1ea3, ch\u00fang t\u00f4i g\u1ed3m:"),
        new("StationCode", 50, 66, 20, PrintFieldAlignment.Left, 11, PrintFieldWeight.Bold),

        new("StaticPerson1Label", 24, 80, 30, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "1. \u00d4ng/b\u00e0:"),
        new("InspectorName1", 53, 80, 75, PrintFieldAlignment.Left, 11, PrintFieldWeight.Bold),
        new("StaticPerson1Role", 131, 80, 45, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "Ch\u1ee9c v\u1ee5: NV P.CLKD"),
        new("StaticPerson2", 24, 90, 152, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "2. \u00d4ng/b\u00e0: ........................................ Ch\u1ee9c v\u1ee5: PX.NX & \u0110B"),
        new("StaticPerson3", 24, 100, 152, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "3. \u00d4ng/b\u00e0: ........................................ Ch\u1ee9c v\u1ee5: VP"),
        new("StaticPerson4", 24, 110, 152, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "4. \u00d4ng/b\u00e0: ........................................ L\u00e1i xe"),

        new("StaticIntro", 18, 125, 174, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, 2, PrintWrapMode.Wrap, LiteralValue: "\u0110\u00e3 c\u00f9ng nhau ti\u1ebfn h\u00e0nh ki\u1ec3m tra h\u00e0ng h\u00f3a tr\u00ean ph\u01b0\u01a1ng ti\u1ec7n, c\u1ee5 th\u1ec3 nh\u01b0 sau:"),
        new("StaticVehicleLabel", 24, 140, 52, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "Bi\u1ec3n ki\u1ec3m so\u00e1t ph\u01b0\u01a1ng ti\u1ec7n:"),
        new("VehiclePlate", 78, 140, 75, PrintFieldAlignment.Left, 11, PrintFieldWeight.Bold),
        new("StaticProductLabel", 24, 151, 26, PrintFieldAlignment.Left, 11, PrintFieldWeight.Normal, LiteralValue: "H\u00e0ng h\u00f3a:"),
        new("ProductNames", 50, 151, 130, PrintFieldAlignment.Left, 11, PrintFieldWeight.Bold, 4, PrintWrapMode.Wrap),

        new("StaticTableHeader", 20, 178, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Bold, LiteralValue: "Stt     N\u1ed9i dung                              Ph\u01b0\u01a1ng th\u1ee9c                         S\u1ed1 l\u01b0\u1ee3ng     \u0110VT"),
        new("StaticTableLine1", 20, 188, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "1       Xu\u1ea5t t\u1ea1i m\u00e1ng \u0111\u00f3ng                  Theo s\u1ed1 \u0111\u1ebfm bao t\u1ef1 \u0111\u1ed9ng"),
        new("StaticTableLine2", 20, 198, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "2       Qua c\u00e2n \u00f4 t\u00f4                         C\u00e2n \u00f4 t\u00f4 \u0111i\u1ec7n t\u1eed"),
        new("ScaleWeightTon", 137, 198, 22, PrintFieldAlignment.Right, 10.5, PrintFieldWeight.Bold),
        new("StaticTableLine3", 20, 208, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "3       Ch\u00eanh l\u1ec7ch (3 = 2 - 1)"),
        new("DifferenceWeightTon", 137, 208, 22, PrintFieldAlignment.Right, 10.5, PrintFieldWeight.Bold),
        new("StaticTableLine4", 20, 218, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "4       T\u1ef7 l\u1ec7 ch\u00eanh l\u1ec7ch % (4 = 3 : 1)"),
        new("DifferencePercent", 137, 218, 22, PrintFieldAlignment.Right, 10.5, PrintFieldWeight.Bold),
        new("StaticTableLine5", 20, 228, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "5       S\u1ed1 ki\u1ec3m \u0111\u1ebfm l\u1ea1i                     Ki\u1ec3m \u0111\u1ebfm h\u00e0ng tr\u00ean xe theo h\u00e0ng, c\u1ed9t"),

        new("StaticComment", 20, 244, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "Nh\u1eadn x\u00e9t: ........................................................................................................"),
        new("StaticReason", 20, 254, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "Nguy\u00ean nh\u00e2n: ....................................................................................................."),
        new("StaticAction", 20, 264, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "Bi\u1ec7n ph\u00e1p x\u1eed l\u00fd: ............................................................................................"),

        new("StaticCopyCount", 20, 278, 170, PrintFieldAlignment.Left, 10.5, PrintFieldWeight.Normal, LiteralValue: "Bi\u00ean b\u1ea3n \u0111\u01b0\u1ee3c l\u1eadp th\u00e0nh 02 b\u1ea3n, c\u00f3 gi\u00e1 tr\u1ecb nh\u01b0 nhau, m\u1ed7i b\u00ean l\u01b0u gi\u1eef 01 b\u1ea3n v\u00e0 c\u00f9ng nhau x\u00e1c nh\u1eadn./."),
        new("StaticSign1", 22, 286, 36, PrintFieldAlignment.Center, 10.5, PrintFieldWeight.Bold, LiteralValue: "P.CLKD"),
        new("StaticSign2", 68, 286, 30, PrintFieldAlignment.Center, 10.5, PrintFieldWeight.Bold, LiteralValue: "VP"),
        new("StaticSign3", 109, 286, 40, PrintFieldAlignment.Center, 10.5, PrintFieldWeight.Bold, LiteralValue: "PX.NX & \u0110B"),
        new("StaticSign4", 158, 286, 34, PrintFieldAlignment.Center, 10.5, PrintFieldWeight.Bold, LiteralValue: "PH\u01af\u01a0NG TI\u1ec6N"),
        new("SalesDepartmentSignerName", 22, 292, 36, PrintFieldAlignment.Center, 10.5, PrintFieldWeight.Bold, 2, PrintWrapMode.Wrap)
    ];
}





