using System.Globalization;
using System.Text;
using System.Text.Json;
using StationApp.Domain.Entities;

namespace StationApp.Application.Services;

public sealed class AuditHistoryRow
{
    public int Index { get; set; }
    public Guid AuditLogId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActionDisplay { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityDisplay { get; set; } = string.Empty;
    public string OldValueDisplay { get; set; } = string.Empty;
    public string NewValueDisplay { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string DetailSummary { get; set; } = string.Empty;
}

public sealed record AuditLogChangeValue(object? Old, object? New, string? Unit = null);

public sealed class AuditLogDetailBuilder
{
    private readonly Dictionary<string, AuditLogChangeValue> _changes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _subject = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _summary = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _notes = new();
    private string? _reason;

    public AuditLogDetailBuilder WithSubject(string key, object? value)
    {
        _subject[key] = value;
        return this;
    }

    public AuditLogDetailBuilder WithReason(string? reason)
    {
        _reason = reason;
        return this;
    }

    public AuditLogDetailBuilder AddChange(string fieldName, object? oldValue, object? newValue, string? unit = null)
    {
        _changes[fieldName] = new AuditLogChangeValue(oldValue, newValue, unit);
        return this;
    }

    public AuditLogDetailBuilder WithSummary(string key, object? value)
    {
        _summary[key] = value;
        return this;
    }

    public AuditLogDetailBuilder AddNote(string? note)
    {
        if (!string.IsNullOrWhiteSpace(note))
        {
            _notes.Add(note.Trim());
        }

        return this;
    }

    public object Build()
        => new
        {
            Subject = _subject.Count == 0 ? null : _subject,
            Reason = _reason,
            Changes = _changes.Count == 0 ? null : _changes.ToDictionary(
                x => x.Key,
                x => new { x.Value.Old, x.Value.New, x.Value.Unit },
                StringComparer.OrdinalIgnoreCase),
            Summary = _summary.Count == 0 ? null : _summary,
            Notes = _notes.Count == 0 ? null : _notes
        };
}

public static class AuditLogDisplayMapper
{
    public static readonly IReadOnlyList<string> KnownActions = new[]
    {
        "EDIT_WEIGHING_SESSION",
        "TRANSFER_EXPORT_TRIP",
        "UPDATE_TEMPORARY_EXPORT_CUT_ORDER",
        "UPDATE_CLAY_VESSEL",
        "TRANSFER_CLAY_TRIP",
        "DELETE_CLAY_TRIP",
        "TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP",
        "TOGGLE_CLAY_RETURNED_TRIP",
        "TOGGLE_DOMESTIC_RETURNED_GOODS",
        "UPDATE_WEIGHING_SESSION_MOOC_NO",
        "UPDATE_WEIGHING_SESSION_SEAL_NO",
        "UPDATE_INCOMING_REGISTRATION",
        "CREATE_INCOMING_SEED_VEHICLE",
        "UPDATE_INCOMING_SEED_VEHICLE",
        "DELETE_INCOMING_SEED_VEHICLE",
        "CREATE_USER_ACCOUNT",
        "UPDATE_USER_ACCOUNT",
        "DEACTIVATE_USER_ACCOUNT",
        "REACTIVATE_USER_ACCOUNT",
        "RESET_USER_PASSWORD",
        "UPDATE_USER_STATION_ASSIGNMENTS",
        "CREATE_STATION",
        "UPDATE_STATION",
        "CAPTURE_WEIGHT_1",
        "CAPTURE_WEIGHT_2",
        "DELETE_WEIGHT_2",
        "CAPTURE_MANUAL_WEIGHT_1",
        "CAPTURE_MANUAL_WEIGHT_2",
        "CREATE_TICKET",
        "COMPLETE_TICKET",
        "CANCEL_VEHICLE_REGISTRATION",
        "CREATE_VEHICLE_REGISTRATION",
        "CREATE_INBOUND_REGISTRATION",
        "CONFIRM_ENTER_WEIGHING",
        "SPLIT_OVERWEIGHT_TICKET",
        "COMPLETE_OVERWEIGHT_WITHOUT_SPLIT",
        "ERP_INBOUND_VALIDATION_FAILED"
    };

    private static readonly HashSet<string> WeightFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Weight1",
        "Weight2",
        "GrossWeight",
        "NetWeight",
        "OldNetWeight",
        "NewNetWeight",
        "StandardTareWeightSnapshot",
        "StandardTareWeight",
        "TtcpWeight",
        "PlannedWeight",
        "ActualReturnedWeight",
        "ReturnedRecognizedWeight",
        "PreviousTripWeight",
        "TareWeightKg",
        "BagWeightKg"
    };

    private static readonly HashSet<string> TechnicalFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "VehicleId",
        "SessionId",
        "PreviousTripSessionId",
        "SourceCutOrderId",
        "TargetCutOrderId",
        "UpdatedCutOrderIds",
        "UpdatedWeighTicketIds",
        "UpdatedDeliveryTicketIds",
        "UpdatedErpCutOrderIds"
    };

    private static readonly HashSet<string> BusinessCodeIdFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ErpCutOrderId",
        "SourceErpCutOrderId",
        "TargetErpCutOrderId"
    };

    private static readonly IReadOnlyDictionary<string, string[]> IdDisplayCompanions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["VehicleId"] = new[] { "VehiclePlate", "InternalVehicleNo" },
            ["SessionId"] = new[] { "SessionNo" },
            ["WeighingSessionId"] = new[] { "SessionNo" },
            ["PreviousTripSessionId"] = new[] { "PreviousTripSessionNo" },
            ["SourceCutOrderId"] = new[] { "SourceDisplayCode", "SourceErpCutOrderId", "SourceVesselName" },
            ["TargetCutOrderId"] = new[] { "TargetDisplayCode", "TargetErpCutOrderId", "TargetVesselName" },
            ["CutOrderId"] = new[] { "DisplayCode", "ErpCutOrderId", "VesselName", "VehiclePlate" },
            ["ProductId"] = new[] { "ProductName", "ProductCode" },
            ["CustomerId"] = new[] { "CustomerName", "CustomerCode" },
            ["StationId"] = new[] { "StationName", "StationCode" },
            ["UserId"] = new[] { "DisplayName", "Username" }
        };

    public static AuditHistoryRow Map(AuditLog log, string? fallbackEntityDisplay = null)
    {
        var row = new AuditHistoryRow
        {
            AuditLogId = log.Id,
            CreatedAt = log.CreatedAt,
            Actor = log.Actor,
            Action = log.Action,
            ActionDisplay = ToActionDisplay(log.Action),
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            EntityDisplay = fallbackEntityDisplay ?? ShortId(log.EntityId),
            OldValueDisplay = "--",
            NewValueDisplay = "--"
        };

        if (string.IsNullOrWhiteSpace(log.DetailJson))
        {
            row.DetailSummary = "Kh\u00f4ng c\u00f3 d\u1eef li\u1ec7u chi ti\u1ebft.";
            return row;
        }

        try
        {
            using var doc = JsonDocument.Parse(log.DetailJson);
            var root = doc.RootElement;

            ApplySubject(row, root);
            ApplyReasonAndNotes(row, root);

            var changes = ExtractChanges(root);
            if (changes.Count > 0)
            {
                row.OldValueDisplay = BuildValueBlock(changes, useOldValue: true);
                row.NewValueDisplay = BuildValueBlock(changes, useOldValue: false);
            }
            else
            {
                row.NewValueDisplay = BuildFallbackPayloadSummary(root);
            }

            row.DetailSummary = BuildDetailSummary(log.Action, root);
        }
        catch (Exception ex)
        {
            row.Note = $"L\u1ed7i \u0111\u1ecdc d\u1eef li\u1ec7u chi ti\u1ebft: {ex.Message}";
            row.DetailSummary = log.DetailJson;
        }

        return row;
    }

    private static void ApplySubject(AuditHistoryRow row, JsonElement root)
    {
        if (root.TryGetProperty("Subject", out var subject) && subject.ValueKind == JsonValueKind.Object)
        {
            if (TryGetString(subject, "Name", out var name)
                || TryGetString(subject, "Code", out name)
                || TryGetString(subject, "SessionNo", out name)
                || TryGetString(subject, "DisplayCode", out name)
                || TryGetString(subject, "VesselName", out name)
                || TryGetString(subject, "VehiclePlate", out name)
                || TryGetString(subject, "Username", out name)
                || TryGetString(subject, "StationCode", out name))
            {
                row.EntityDisplay = name;
                return;
            }
        }

        if (TryGetString(root, "SessionNo", out var sessionNo)
            || TryGetString(root, "DisplayCode", out sessionNo)
            || TryGetString(root, "VesselName", out sessionNo)
            || TryGetString(root, "VehiclePlate", out sessionNo)
            || TryGetString(root, "Username", out sessionNo)
            || TryGetString(root, "DisplayName", out sessionNo)
            || TryGetString(root, "StationName", out sessionNo)
            || TryGetString(root, "StationCode", out sessionNo)
            || TryGetString(root, "ProductName", out sessionNo)
            || TryGetString(root, "CustomerName", out sessionNo))
        {
            row.EntityDisplay = sessionNo;
        }
    }

    private static void ApplyReasonAndNotes(AuditHistoryRow row, JsonElement root)
    {
        if (TryGetString(root, "Reason", out var reason)
            || TryGetString(root, "Note", out reason))
        {
            row.Note = reason;
        }

        if (root.TryGetProperty("Notes", out var notes) && notes.ValueKind == JsonValueKind.Array)
        {
            var noteLines = notes.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (noteLines.Length > 0)
            {
                row.Note = string.IsNullOrWhiteSpace(row.Note)
                    ? string.Join(Environment.NewLine, noteLines)
                    : row.Note + Environment.NewLine + string.Join(Environment.NewLine, noteLines);
            }
        }
    }

    private static List<AuditFieldChange> ExtractChanges(JsonElement root)
    {
        var changes = new List<AuditFieldChange>();

        if (root.TryGetProperty("Changes", out var changesProp) && changesProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in changesProp.EnumerateObject())
            {
                if (IsTechnicalField(property.Name) || property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!property.Value.TryGetProperty("Old", out var oldValue)
                    && !property.Value.TryGetProperty("old", out oldValue))
                {
                    oldValue = default;
                }

                if (!property.Value.TryGetProperty("New", out var newValue)
                    && !property.Value.TryGetProperty("new", out newValue))
                {
                    newValue = default;
                }

                var unit = property.Value.TryGetProperty("Unit", out var unitProp)
                    || property.Value.TryGetProperty("unit", out unitProp)
                    ? unitProp.GetString()
                    : null;

                changes.Add(new AuditFieldChange(property.Name, oldValue, newValue, unit));
            }

            return changes;
        }

        if (root.TryGetProperty("Old", out var oldProp)
            && root.TryGetProperty("New", out var newProp)
            && oldProp.ValueKind == JsonValueKind.Object
            && newProp.ValueKind == JsonValueKind.Object)
        {
            var names = oldProp.EnumerateObject().Select(x => x.Name)
                .Concat(newProp.EnumerateObject().Select(x => x.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (IsTechnicalField(name))
                {
                    continue;
                }

                var hasOld = oldProp.TryGetProperty(name, out var oldValue);
                var hasNew = newProp.TryGetProperty(name, out var newValue);
                if (!hasOld && !hasNew)
                {
                    continue;
                }

                if (JsonValuesEqual(oldValue, newValue))
                {
                    continue;
                }

                changes.Add(new AuditFieldChange(name, oldValue, newValue, null));
            }

            return changes;
        }

        changes.AddRange(ExtractOldNewPrefixPairs(root));
        changes.AddRange(ExtractReturnedBrokenTripChanges(root));

        return Deduplicate(changes);
    }

    private static IEnumerable<AuditFieldChange> ExtractOldNewPrefixPairs(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var properties = root.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            if (!property.Key.StartsWith("Old", StringComparison.OrdinalIgnoreCase) || property.Key.Length <= 3)
            {
                continue;
            }

            var fieldName = property.Key[3..];
            if (IsTechnicalField(fieldName))
            {
                continue;
            }

            if (properties.TryGetValue("New" + fieldName, out var newValue))
            {
                yield return new AuditFieldChange(fieldName, property.Value, newValue, null);
            }
        }
    }

    private static IEnumerable<AuditFieldChange> ExtractReturnedBrokenTripChanges(JsonElement root)
    {
        if (root.TryGetProperty("OldIsReturnedBrokenTrip", out var oldReturned)
            && root.TryGetProperty("NewIsReturnedBrokenTrip", out var newReturned))
        {
            yield return new AuditFieldChange("IsReturnedBrokenTrip", oldReturned, newReturned, null);
        }

        if (root.TryGetProperty("ActualReturnedWeight", out var actualReturned)
            && root.TryGetProperty("ReturnedRecognizedWeight", out var recognizedReturned))
        {
            yield return new AuditFieldChange("ReturnedWeight", actualReturned, recognizedReturned, "kg");
        }
    }

    private static List<AuditFieldChange> Deduplicate(IEnumerable<AuditFieldChange> changes)
    {
        var result = new List<AuditFieldChange>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            if (seen.Add(change.FieldName))
            {
                result.Add(change);
            }
        }

        return result;
    }

    private static string BuildValueBlock(IEnumerable<AuditFieldChange> changes, bool useOldValue)
    {
        var lines = changes
            .Where(x => !IsTechnicalField(x.FieldName))
            .Select(x => $"{ToFieldDisplay(x.FieldName, useOldValue)}: {FormatValue(x.FieldName, useOldValue ? x.OldValue : x.NewValue, x.Unit)}")
            .ToArray();

        return lines.Length == 0 ? "--" : string.Join(Environment.NewLine, lines);
    }

    private static string BuildFallbackPayloadSummary(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return FormatValue("Detail", root, null);
        }

        var lines = new List<string>();
        var renderedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                continue;
            }

            if (IsOpaqueIdField(property.Name))
            {
                if (TryResolveMeaningfulIdDisplay(root, property.Name, renderedFields, out var displayField, out var displayValue))
                {
                    lines.Add($"{ToFieldDisplay(displayField)}: {displayValue}");
                    renderedFields.Add(displayField);
                }

                continue;
            }

            if (IsTechnicalField(property.Name) || renderedFields.Contains(property.Name))
            {
                continue;
            }

            lines.Add($"{ToFieldDisplay(property.Name)}: {FormatValue(property.Name, property.Value, null)}");
            renderedFields.Add(property.Name);
            if (lines.Count >= 8)
            {
                break;
            }
        }

        return lines.Count == 0 ? "--" : string.Join(Environment.NewLine, lines);
    }

    private static string BuildDetailSummary(string action, JsonElement root)
    {
        var details = new List<string>();

        var updatedSessionCount = TryGetInt(root, "UpdatedSessionCount");
        var updatedLineCount = TryGetInt(root, "UpdatedLineCount");
        if (updatedSessionCount.HasValue || updatedLineCount.HasValue)
        {
            details.Add($"\u0110\u00e3 c\u1eadp nh\u1eadt {updatedSessionCount ?? 0:N0} chuy\u1ebfn xe, {updatedLineCount ?? 0:N0} d\u00f2ng chuy\u1ebfn");
        }
        else if (updatedLineCount.HasValue)
        {
            details.Add($"\u0110\u00e3 c\u1eadp nh\u1eadt {updatedLineCount.Value:N0} d\u00f2ng chuy\u1ebfn");
        }

        if (TryGetString(root, "SourceDisplayCode", out var source)
            && TryGetString(root, "TargetDisplayCode", out var target))
        {
            details.Add($"Chuy\u1ec3n t\u1eeb {source} sang {target}");
        }
        else if (TryGetString(root, "SourceErpCutOrderId", out source)
                 && TryGetString(root, "TargetErpCutOrderId", out target))
        {
            details.Add($"Chuy\u1ec3n t\u1eeb {source} sang {target}");
        }

        if (root.TryGetProperty("InvalidatedOldVehicleStandardTare", out var invalidated)
            && invalidated.ValueKind == JsonValueKind.Object
            && TryGetString(invalidated, "VehiclePlate", out var oldPlate))
        {
            var weight = invalidated.TryGetProperty("InvalidatedWeight", out var weightProp)
                ? FormatValue("StandardTareWeight", weightProp, "kg")
                : "--";
            details.Add($"V\u00f4 hi\u1ec7u TL b\u00ec xe c\u0169 {oldPlate}: {weight}");
        }

        if (root.TryGetProperty("AppliedStandardTareToNewVehicle", out var applied)
            && applied.ValueKind == JsonValueKind.Object
            && TryGetString(applied, "VehiclePlate", out var newPlate))
        {
            var weight = applied.TryGetProperty("StandardTareWeight", out var weightProp)
                ? FormatValue("StandardTareWeight", weightProp, "kg")
                : "--";
            details.Add($"\u00c1p TL b\u00ec cho xe m\u1edbi {newPlate}: {weight}");
        }

        if (root.TryGetProperty("IsReturnedWeightCapped", out var capped)
            && capped.ValueKind is JsonValueKind.True or JsonValueKind.False
            && capped.GetBoolean())
        {
            details.Add("TL ho\u00e0n ghi nh\u1eadn \u0111\u00e3 \u0111\u01b0\u1ee3c gi\u1edbi h\u1ea1n theo chuy\u1ebfn g\u1ea7n nh\u1ea5t");
        }

        if (details.Count == 0 && TryGetString(root, "Summary", out var summaryText))
        {
            details.Add(summaryText);
        }

        return details.Count == 0 ? string.Empty : string.Join(Environment.NewLine, details);
    }

    private static bool IsTechnicalField(string fieldName)
        => TechnicalFields.Contains(fieldName) || IsOpaqueIdField(fieldName);

    private static bool IsOpaqueIdField(string fieldName)
    {
        if (BusinessCodeIdFields.Contains(fieldName))
        {
            return false;
        }

        return string.Equals(fieldName, "Id", StringComparison.OrdinalIgnoreCase)
               || fieldName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
               || fieldName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase)
               || fieldName.EndsWith("Guid", StringComparison.OrdinalIgnoreCase)
               || fieldName.EndsWith("Guids", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveMeaningfulIdDisplay(
        JsonElement root,
        string idFieldName,
        ISet<string> renderedFields,
        out string displayField,
        out string displayValue)
    {
        displayField = string.Empty;
        displayValue = string.Empty;

        if (!IdDisplayCompanions.TryGetValue(idFieldName, out var companionFields))
        {
            var baseName = TrimIdSuffix(idFieldName);
            companionFields = new[]
            {
                baseName + "Name",
                baseName + "Code",
                baseName + "No",
                baseName + "DisplayCode"
            };
        }

        foreach (var companionField in companionFields)
        {
            if (renderedFields.Contains(companionField))
            {
                return false;
            }

            if (TryGetString(root, companionField, out var value))
            {
                displayField = companionField;
                displayValue = value;
                return true;
            }
        }

        return false;
    }

    private static string TrimIdSuffix(string fieldName)
    {
        if (fieldName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase))
        {
            return fieldName[..^3];
        }

        if (fieldName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
        {
            return fieldName[..^2];
        }

        if (fieldName.EndsWith("Guids", StringComparison.OrdinalIgnoreCase))
        {
            return fieldName[..^5];
        }

        return fieldName.EndsWith("Guid", StringComparison.OrdinalIgnoreCase)
            ? fieldName[..^4]
            : fieldName;
    }

    public static string ToActionDisplay(string action)
        => action switch
        {
            "EDIT_WEIGHING_SESSION" => "\u0110\u1ed5i s\u1ed1 xe",
            "TRANSFER_EXPORT_TRIP" => "Chuy\u1ec3n chuy\u1ebfn xe xu\u1ea5t kh\u1ea9u",
            "UPDATE_TEMPORARY_EXPORT_CUT_ORDER" => "S\u1eeda c\u1eaft l\u1ec7nh t\u1ea1m xu\u1ea5t kh\u1ea9u",
            "UPDATE_CLAY_VESSEL" => "S\u1eeda t\u00e0u m\u1ecf s\u00e9t",
            "TRANSFER_CLAY_TRIP" => "Chuy\u1ec3n chuy\u1ebfn xe m\u1ecf s\u00e9t",
            "DELETE_CLAY_TRIP" => "X\u00f3a chuy\u1ebfn xe m\u1ecf s\u00e9t",
            "TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP" => "C\u1eadp nh\u1eadt h\u00e0ng ho\u00e0n m\u1ecf \u0111\u00e1",
            "TOGGLE_CLAY_RETURNED_TRIP" => "C\u1eadp nh\u1eadt h\u00e0ng ho\u00e0n m\u1ecf s\u00e9t",
            "TOGGLE_DOMESTIC_RETURNED_GOODS" => "Cập nhật hoàn hàng nội địa",
            "UPDATE_WEIGHING_SESSION_MOOC_NO" => "C\u1eadp nh\u1eadt s\u1ed1 mooc",
            "UPDATE_WEIGHING_SESSION_SEAL_NO" => "C\u1eadp nh\u1eadt s\u1ed1 seal",
            "UPDATE_INCOMING_REGISTRATION" => "C\u1eadp nh\u1eadt c\u1eaft l\u1ec7nh xe v\u00e0o",
            "CREATE_INCOMING_SEED_VEHICLE" => "Tạo xe nhập mẫu",
            "UPDATE_INCOMING_SEED_VEHICLE" => "Sửa xe nhập mẫu",
            "DELETE_INCOMING_SEED_VEHICLE" => "Xóa xe nhập mẫu",
            "CREATE_USER_ACCOUNT" => "T\u1ea1o t\u00e0i kho\u1ea3n",
            "UPDATE_USER_ACCOUNT" => "S\u1eeda t\u00e0i kho\u1ea3n",
            "DEACTIVATE_USER_ACCOUNT" => "Ng\u1eebng ho\u1ea1t \u0111\u1ed9ng t\u00e0i kho\u1ea3n",
            "REACTIVATE_USER_ACCOUNT" => "K\u00edch ho\u1ea1t l\u1ea1i t\u00e0i kho\u1ea3n",
            "SET_USER_ACTIVE_STATUS" => "C\u1eadp nh\u1eadt tr\u1ea1ng th\u00e1i t\u00e0i kho\u1ea3n",
            "RESET_USER_PASSWORD" => "Reset m\u1eadt kh\u1ea9u",
            "UPDATE_USER_STATION_ASSIGNMENTS" => "C\u1eadp nh\u1eadt ph\u00e2n quy\u1ec1n tr\u1ea1m",
            "CREATE_USER_STATION_ASSIGNMENT" => "T\u1ea1o ph\u00e2n quy\u1ec1n tr\u1ea1m",
            "UPDATE_USER_STATION_ASSIGNMENT" => "C\u1eadp nh\u1eadt ph\u00e2n quy\u1ec1n tr\u1ea1m",
            "CREATE_STATION" => "T\u1ea1o tr\u1ea1m",
            "UPDATE_STATION" => "S\u1eeda tr\u1ea1m",
            "UPDATE_STATION_FEATURES" => "C\u1eadp nh\u1eadt ch\u1ee9c n\u0103ng tr\u1ea1m",
            "CAPTURE_WEIGHT_1" => "Ghi nh\u1eadn c\u00e2n l\u1ea7n 1",
            "CAPTURE_WEIGHT_2" => "Ghi nh\u1eadn c\u00e2n l\u1ea7n 2",
            "DELETE_WEIGHT_2" => "X\u00f3a l\u01b0\u1ee3t c\u00e2n l\u1ea7n 2",
            "CAPTURE_MANUAL_WEIGHT_1" => "Cân tay lần 1",
            "CAPTURE_MANUAL_WEIGHT_2" => "Cân tay lần 2",
            "CREATE_TICKET" => "T\u1ea1o phi\u1ebfu c\u00e2n",
            "COMPLETE_TICKET" => "Ho\u00e0n t\u1ea5t phi\u1ebfu c\u00e2n",
            "CANCEL_VEHICLE_REGISTRATION" => "H\u1ee7y c\u1eaft l\u1ec7nh xe",
            "CREATE_VEHICLE_REGISTRATION" => "T\u1ea1o c\u1eaft l\u1ec7nh xe",
            "CREATE_INBOUND_REGISTRATION" => "T\u1ea1o c\u1eaft l\u1ec7nh nh\u1eadp h\u00e0ng",
            "CONFIRM_ENTER_WEIGHING" => "X\u00e1c nh\u1eadn xe v\u00e0o c\u00e2n",
            "SPLIT_OVERWEIGHT_TICKET" => "T\u00e1ch t\u1ea3i qu\u00e1 t\u1ea3i",
            "COMPLETE_OVERWEIGHT_WITHOUT_SPLIT" => "X\u00e1c nh\u1eadn qu\u00e1 t\u1ea3i kh\u00f4ng t\u00e1ch",
            "ERP_INBOUND_VALIDATION_FAILED" => "L\u1ed7i ki\u1ec3m tra d\u1eef li\u1ec7u ERP",
            _ => action
        };
    private static string ToFieldDisplay(string fieldName, bool useOldValue = false)
        => fieldName switch
        {
            "VehiclePlate" => "S\u1ed1 xe",
            "InternalVehicleNo" => "S\u1ed1 xe n\u1ed9i b\u1ed9",
            "SessionNo" => "S\u1ed1 l\u01b0\u1ee3t c\u00e2n",
            "DisplayCode" => "M\u00e3 hi\u1ec3n th\u1ecb",
            "CutOrder" => "C\u1eaft l\u1ec7nh",
            "SourceDisplayCode" => "C\u1eaft l\u1ec7nh ngu\u1ed3n",
            "TargetDisplayCode" => "C\u1eaft l\u1ec7nh \u0111\u00edch",
            "SourceErpCutOrderId" => "C\u1eaft l\u1ec7nh ngu\u1ed3n",
            "TargetErpCutOrderId" => "C\u1eaft l\u1ec7nh \u0111\u00edch",
            "ErpCutOrderId" => "M\u00e3 c\u1eaft l\u1ec7nh ERP",
            "SourceVesselName" => "T\u00e0u ngu\u1ed3n",
            "TargetVesselName" => "T\u00e0u \u0111\u00edch",
            "VesselName" => "T\u00e0u/S\u00e0 lan",
            "PreviousTripSessionNo" => "Chuy\u1ebfn g\u1ea7n nh\u1ea5t",
            "StandardTareWeightSnapshot" => "TL b\u00ec",
            "StandardTareWeight" => "TL b\u00ec",
            "TtcpWeight" => "TL b\u00ec",
            "Weight1" => "C\u00e2n l\u1ea7n 1",
            "Weight2" => "C\u00e2n l\u1ea7n 2",
            "GrossWeight" => "TL t\u1ed5ng",
            "NetWeight" => "TL h\u00e0ng",
            "OldNetWeight" => "TL h\u00e0ng",
            "NewNetWeight" => "TL h\u00e0ng",
            "ReturnedWeight" => useOldValue ? "TL ho\u00e0n th\u1ef1c c\u00e2n" : "TL ho\u00e0n ghi nh\u1eadn",
            "ActualReturnedWeight" => "TL ho\u00e0n th\u1ef1c c\u00e2n",
            "ReturnedRecognizedWeight" => "TL ho\u00e0n ghi nh\u1eadn",
            "PreviousTripWeight" => "TL h\u00e0ng chuy\u1ebfn g\u1ea7n nh\u1ea5t",
            "CustomerCode" => "M\u00e3 kh\u00e1ch h\u00e0ng",
            "CustomerName" => "Kh\u00e1ch h\u00e0ng",
            "ReceiverName" => "T\u00e0i x\u1ebf/\u0110\u1ea1i di\u1ec7n",
            "ProductCode" => "M\u00e3 h\u00e0ng",
            "ProductName" => "H\u00e0ng h\u00f3a",
            "ProductType" => "Lo\u1ea1i h\u00e0ng",
            "TransactionType" => "Lo\u1ea1i giao d\u1ecbch",
            "PlannedWeight" => "SL k\u1ebf ho\u1ea1ch",
            "BagCount" => "S\u1ed1 bao",
            "TareWeightKg" => "TL v\u1ecf",
            "BagWeightKg" => "TL bao",
            "ExportPackageType" => "Lo\u1ea1i xu\u1ea5t kh\u1ea9u",
            "Notes" => "Ghi ch\u00fa",
            "SealNo" => "S\u1ed1 seal",
            "MoocNumber" => "S\u1ed1 mooc",
            "IsReturnedBrokenTrip" => "H\u00e0ng ho\u00e0n",
            "IsCancelled" => "Tr\u1ea1ng th\u00e1i h\u1ee7y",
            "IsDeleted" => "Tr\u1ea1ng th\u00e1i x\u00f3a",
            "IsActive" => "Tr\u1ea1ng th\u00e1i ho\u1ea1t \u0111\u1ed9ng",
            "RoleCode" => "Vai tr\u00f2",
            "Password" => "M\u1eadt kh\u1ea9u",
            "StationAssignments" => "Ph\u00e2n quy\u1ec1n tr\u1ea1m",
            "Username" => "T\u00ean \u0111\u0103ng nh\u1eadp",
            "DisplayName" => "T\u00ean hi\u1ec3n th\u1ecb",
            "StationCode" => "M\u00e3 tr\u1ea1m",
            "StationName" => "T\u00ean tr\u1ea1m",
            "SortOrder" => "Th\u1ee9 t\u1ef1 hi\u1ec3n th\u1ecb",
            _ => SplitPascalCase(fieldName)
        };
    private static string FormatValue(string fieldName, JsonElement value, string? unit)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "--";
        }

        return FormatScalar(fieldName, value, unit);
    }

    private static string FormatScalar(string fieldName, JsonElement value, string? unit)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            var boolValue = value.GetBoolean();
            if (fieldName.Contains("Returned", StringComparison.OrdinalIgnoreCase)
                || fieldName.Contains("Active", StringComparison.OrdinalIgnoreCase))
            {
                return boolValue ? "C\u00f3" : "Kh\u00f4ng";
            }

            return boolValue ? "C\u00f3" : "Kh\u00f4ng";
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetDecimal(out var decimalValue))
            {
                if (string.Equals(unit, "kg", StringComparison.OrdinalIgnoreCase) || WeightFields.Contains(fieldName))
                {
                    return $"{decimalValue:N0} kg";
                }

                return decimalValue % 1 == 0
                    ? decimalValue.ToString("N0", CultureInfo.CurrentCulture)
                    : decimalValue.ToString("N3", CultureInfo.CurrentCulture);
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "--";
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
            {
                return dateTime.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);
            }

            return text;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var values = value.EnumerateArray()
                .Where(x => x.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : FormatScalar(fieldName, x, unit))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            return values.Length == 0 ? "--" : string.Join(", ", values);
        }

        return value.ToString();
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        value = property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static int? TryGetInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetInt32(out var value) ? value : null;
    }

    private static bool JsonValuesEqual(JsonElement oldValue, JsonElement newValue)
        => oldValue.ValueKind == newValue.ValueKind
           && string.Equals(oldValue.ToString(), newValue.ToString(), StringComparison.Ordinal);

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private sealed record AuditFieldChange(string FieldName, JsonElement OldValue, JsonElement NewValue, string? Unit);
}
