using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{

    public sealed class DimensionTemplateCustomizerFieldEditPreview
    {
        public DimensionTemplateCustomizerFieldEditPreview(
            DimensionTemplateCustomizerFieldEditPreviewState state,
            string code,
            string message,
            bool canApply,
            bool hasWarning,
            string warning,
            string normalizedValue,
            DimensionBounds parsedBounds,
            bool hasParsedBounds,
            int parsedInteger,
            bool hasParsedInteger,
            bool parsedBoolean,
            bool hasParsedBoolean,
            DimensionTemplateCustomizerField field,
            DimensionTemplateCustomizerFieldSet fieldSet)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanApply = canApply;
            HasWarning = hasWarning;
            Warning = warning ?? string.Empty;
            NormalizedValue = normalizedValue ?? string.Empty;
            ParsedBounds = parsedBounds;
            HasParsedBounds = hasParsedBounds;
            ParsedInteger = parsedInteger;
            HasParsedInteger = hasParsedInteger;
            ParsedBoolean = parsedBoolean;
            HasParsedBoolean = hasParsedBoolean;
            Field = field;
            FieldSet = fieldSet;
        }

        public DimensionTemplateCustomizerFieldEditPreviewState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanApply { get; private set; }

        public bool HasWarning { get; private set; }

        public string Warning { get; private set; }

        public string NormalizedValue { get; private set; }

        public DimensionBounds ParsedBounds { get; private set; }

        public bool HasParsedBounds { get; private set; }

        public int ParsedInteger { get; private set; }

        public bool HasParsedInteger { get; private set; }

        public bool ParsedBoolean { get; private set; }

        public bool HasParsedBoolean { get; private set; }

        public DimensionTemplateCustomizerField Field { get; private set; }

        public DimensionTemplateCustomizerFieldSet FieldSet { get; private set; }
    }

    public static class DimensionTemplateCustomizerFieldEditPreviewUtility
    {
        public static DimensionTemplateCustomizerFieldEditPreview Preview(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            if (workspace == null)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.MissingWorkspace,
                    "workspace-missing",
                    "Select or create a Dimension Asset before editing fields.",
                    false,
                    false,
                    string.Empty,
                    request == null ? string.Empty : request.NewValue,
                    null,
                    null);
            }

            DimensionTemplateCustomizerFieldSet fieldSet =
                workspace.GetFieldSet(request == null
                    ? DimensionTemplateCustomizerFocusRequest.ForSection("overview")
                    : request.FocusRequest);
            if (fieldSet == null)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.MissingFieldSet,
                    "field-set-missing",
                    "The selected customizer target does not expose editable fields.",
                    false,
                    false,
                    string.Empty,
                    request == null ? string.Empty : request.NewValue,
                    null,
                    null);
            }

            DimensionTemplateCustomizerField field =
                FindField(fieldSet.Fields, request == null ? string.Empty : request.FieldId);
            if (field == null)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.MissingField,
                    "field-missing",
                    "The requested field is not available on the selected customizer target.",
                    false,
                    false,
                    string.Empty,
                    request == null ? string.Empty : request.NewValue,
                    null,
                    fieldSet);
            }

            if (field.Editability == DimensionTemplateCustomizerFieldEditability.ReadOnly)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.ReadOnly,
                    "field-read-only",
                    "This field is informational and cannot be edited directly.",
                    false,
                    false,
                    string.Empty,
                    request == null ? string.Empty : request.NewValue,
                    field,
                    fieldSet);
            }

            if (field.Editability == DimensionTemplateCustomizerFieldEditability.RequiresEditorImplementation)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.RequiresEditorImplementation,
                    "field-editor-implementation-needed",
                    "This field can be edited only after a dedicated editor implementation is added.",
                    false,
                    false,
                    string.Empty,
                    request == null ? string.Empty : request.NewValue,
                    field,
                    fieldSet);
            }

            if (field.Editability == DimensionTemplateCustomizerFieldEditability.RuntimeOnly)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.RuntimeOnly,
                    "field-runtime-only",
                    "This field is controlled by runtime state and cannot be edited from authoring UI.",
                    false,
                    false,
                    string.Empty,
                    request == null ? string.Empty : request.NewValue,
                    field,
                    fieldSet);
            }

            string normalizedValue = NormalizeValue(field, request == null ? string.Empty : request.NewValue);
            if (field.Required && string.IsNullOrEmpty(normalizedValue))
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.MissingValue,
                    "field-required",
                    field.Label + " is required.",
                    false,
                    true,
                    field.Label + " cannot be empty.",
                    normalizedValue,
                    field,
                    fieldSet);
            }

            DimensionTemplateCustomizerFieldEditPreview parsedPreview;
            if (!TryValidateValue(field, fieldSet, normalizedValue, out parsedPreview))
            {
                return parsedPreview;
            }

            string warning = BuildWarning(field, normalizedValue);
            bool hasWarning = !string.IsNullOrEmpty(warning);
            bool accepted = request != null && request.AcceptWarnings;
            if (hasWarning && !accepted)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.Warning,
                    "field-warning",
                    "This edit is valid, but the customizer should ask for confirmation.",
                    false,
                    true,
                    warning,
                    normalizedValue,
                    field,
                    fieldSet);
            }

            return CreateResult(
                DimensionTemplateCustomizerFieldEditPreviewState.Ready,
                "field-ready",
                "This field edit is ready for a future editor implementation to apply.",
                true,
                hasWarning,
                warning,
                normalizedValue,
                field,
                fieldSet,
                parsedPreview);
        }

        private static DimensionTemplateCustomizerField FindField(
            IReadOnlyList<DimensionTemplateCustomizerField> fields,
            string fieldId)
        {
            if (fields == null || string.IsNullOrEmpty(fieldId))
            {
                return null;
            }

            for (int i = 0; i < fields.Count; i++)
            {
                DimensionTemplateCustomizerField field = fields[i];
                if (field != null && field.FieldId == fieldId)
                {
                    return field;
                }
            }

            return null;
        }

        private static string NormalizeValue(
            DimensionTemplateCustomizerField field,
            string value)
        {
            if (field == null)
            {
                return value ?? string.Empty;
            }

            if (field.Kind == DimensionTemplateCustomizerFieldKind.Text ||
                field.Kind == DimensionTemplateCustomizerFieldKind.Integer ||
                field.Kind == DimensionTemplateCustomizerFieldKind.Boolean ||
                field.Kind == DimensionTemplateCustomizerFieldKind.Reference)
            {
                return (value ?? string.Empty).Trim();
            }

            return value ?? string.Empty;
        }

        private static bool TryValidateValue(
            DimensionTemplateCustomizerField field,
            DimensionTemplateCustomizerFieldSet fieldSet,
            string normalizedValue,
            out DimensionTemplateCustomizerFieldEditPreview preview)
        {
            preview = null;
            if (field == null)
            {
                return false;
            }

            if (field.Kind == DimensionTemplateCustomizerFieldKind.Integer)
            {
                int value;
                if (!int.TryParse(normalizedValue, out value))
                {
                    preview = CreateResult(
                        DimensionTemplateCustomizerFieldEditPreviewState.InvalidFormat,
                        "integer-invalid",
                        field.Label + " must be a whole number.",
                        false,
                        true,
                        "Enter a whole number.",
                        normalizedValue,
                        field,
                        fieldSet);
                    return false;
                }

                if (field.FieldId == "minimum-api-version" && value < 1)
                {
                    preview = CreateResult(
                        DimensionTemplateCustomizerFieldEditPreviewState.InvalidFormat,
                        "minimum-api-version-invalid",
                        "Minimum API version must be at least 1.",
                        false,
                        true,
                        "Use 1 or a newer supported API version.",
                        normalizedValue,
                        field,
                        fieldSet);
                    return false;
                }

                preview = CreateIntegerCarrier(value, normalizedValue, field, fieldSet);
                return true;
            }

            if (field.Kind == DimensionTemplateCustomizerFieldKind.Boolean)
            {
                bool value;
                if (!TryParseBoolean(normalizedValue, out value))
                {
                    preview = CreateResult(
                        DimensionTemplateCustomizerFieldEditPreviewState.InvalidFormat,
                        "boolean-invalid",
                        field.Label + " must be yes/no, true/false, enabled/disabled, or 1/0.",
                        false,
                        true,
                        "Use yes/no, true/false, enabled/disabled, or 1/0.",
                        normalizedValue,
                        field,
                        fieldSet);
                    return false;
                }

                preview = CreateBooleanCarrier(value, normalizedValue, field, fieldSet);
                return true;
            }

            if (field.Kind == DimensionTemplateCustomizerFieldKind.Bounds)
            {
                DimensionBounds bounds;
                if (!TryParseBounds(normalizedValue, out bounds))
                {
                    preview = CreateResult(
                        DimensionTemplateCustomizerFieldEditPreviewState.InvalidFormat,
                        "bounds-invalid",
                        field.Label + " must be entered as minX,minY,maxX,maxY.",
                        false,
                        true,
                        "Use minX,minY,maxX,maxY. Example: -8,-8,8,8.",
                        normalizedValue,
                        field,
                        fieldSet);
                    return false;
                }

                if (bounds.Size.x <= 0 || bounds.Size.y <= 0)
                {
                    preview = CreateResult(
                        DimensionTemplateCustomizerFieldEditPreviewState.InvalidFormat,
                        "bounds-empty",
                        field.Label + " must have a positive width and height.",
                        false,
                        true,
                        "Max values must be larger than min values.",
                        normalizedValue,
                        field,
                        fieldSet);
                    return false;
                }

                preview = CreateBoundsCarrier(bounds, normalizedValue, field, fieldSet);
                return true;
            }

            if (field.Kind == DimensionTemplateCustomizerFieldKind.Reference &&
                string.IsNullOrEmpty(normalizedValue))
            {
                preview = CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.MissingValue,
                    "reference-missing",
                    field.Label + " needs a target asset or reference ID.",
                    false,
                    true,
                    "Assign a target asset/reference before applying this edit.",
                    normalizedValue,
                    field,
                    fieldSet);
                return false;
            }

            if ((field.FieldId == "dimension-id" ||
                    field.FieldId == "biome-id" ||
                    field.FieldId == "content-pack-id") &&
                !IsSafeIdentifier(normalizedValue))
            {
                preview = CreateResult(
                    DimensionTemplateCustomizerFieldEditPreviewState.InvalidFormat,
                    "identifier-invalid",
                    field.Label + " can use letters, numbers, dot, underscore, colon, or dash.",
                    false,
                    true,
                    "Avoid spaces and punctuation that can break manifest IDs.",
                    normalizedValue,
                    field,
                    fieldSet);
                return false;
            }

            preview = CreateResult(
                DimensionTemplateCustomizerFieldEditPreviewState.Ready,
                "value-valid",
                "Value is valid.",
                true,
                false,
                string.Empty,
                normalizedValue,
                field,
                fieldSet);
            return true;
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "yes" ||
                normalized == "true" ||
                normalized == "enabled" ||
                normalized == "on" ||
                normalized == "1")
            {
                result = true;
                return true;
            }

            if (normalized == "no" ||
                normalized == "false" ||
                normalized == "disabled" ||
                normalized == "off" ||
                normalized == "0")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }

        private static bool TryParseBounds(string value, out DimensionBounds bounds)
        {
            bounds = new DimensionBounds(new int2(0, 0), new int2(0, 0));
            string normalized = (value ?? string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Replace("->", ",")
                .Replace("x", ",")
                .Replace("X", ",");
            string[] parts = normalized.Split(',');
            List<int> numbers = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = (parts[i] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                int valuePart;
                if (!int.TryParse(part, out valuePart))
                {
                    return false;
                }

                numbers.Add(valuePart);
            }

            if (numbers.Count < 4)
            {
                return false;
            }

            bounds = new DimensionBounds(
                new int2(numbers[0], numbers[1]),
                new int2(numbers[2], numbers[3]));
            return true;
        }

        private static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool valid =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '.' ||
                    c == '_' ||
                    c == ':' ||
                    c == '-';
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildWarning(
            DimensionTemplateCustomizerField field,
            string normalizedValue)
        {
            if (field == null)
            {
                return string.Empty;
            }

            if (field.FieldId == "dimension-id" ||
                field.FieldId == "biome-id" ||
                field.FieldId == "content-pack-id")
            {
                return "Changing IDs can break references from manifests, saved registries, portals, or other templates.";
            }

            if (field.Kind == DimensionTemplateCustomizerFieldKind.Bounds)
            {
                return "Changing bounds can move generated content, coordinate shells, map previews, or validation results.";
            }

            if (field.FieldId == "enabled" &&
                IsFalseLike(normalizedValue))
            {
                return "Disabling content can make generation plans lose their target biome/content.";
            }

            return string.Empty;
        }

        private static bool IsFalseLike(string value)
        {
            bool parsed;
            return TryParseBoolean(value, out parsed) && !parsed;
        }

        private static DimensionTemplateCustomizerFieldEditPreview CreateIntegerCarrier(
            int value,
            string normalizedValue,
            DimensionTemplateCustomizerField field,
            DimensionTemplateCustomizerFieldSet fieldSet)
        {
            return new DimensionTemplateCustomizerFieldEditPreview(
                DimensionTemplateCustomizerFieldEditPreviewState.Ready,
                "integer-valid",
                "Integer value is valid.",
                true,
                false,
                string.Empty,
                normalizedValue,
                new DimensionBounds(new int2(0, 0), new int2(0, 0)),
                false,
                value,
                true,
                false,
                false,
                field,
                fieldSet);
        }

        private static DimensionTemplateCustomizerFieldEditPreview CreateBooleanCarrier(
            bool value,
            string normalizedValue,
            DimensionTemplateCustomizerField field,
            DimensionTemplateCustomizerFieldSet fieldSet)
        {
            return new DimensionTemplateCustomizerFieldEditPreview(
                DimensionTemplateCustomizerFieldEditPreviewState.Ready,
                "boolean-valid",
                "Boolean value is valid.",
                true,
                false,
                string.Empty,
                normalizedValue,
                new DimensionBounds(new int2(0, 0), new int2(0, 0)),
                false,
                0,
                false,
                value,
                true,
                field,
                fieldSet);
        }

        private static DimensionTemplateCustomizerFieldEditPreview CreateBoundsCarrier(
            DimensionBounds bounds,
            string normalizedValue,
            DimensionTemplateCustomizerField field,
            DimensionTemplateCustomizerFieldSet fieldSet)
        {
            return new DimensionTemplateCustomizerFieldEditPreview(
                DimensionTemplateCustomizerFieldEditPreviewState.Ready,
                "bounds-valid",
                "Bounds value is valid.",
                true,
                false,
                string.Empty,
                normalizedValue,
                bounds,
                true,
                0,
                false,
                false,
                false,
                field,
                fieldSet);
        }

        private static DimensionTemplateCustomizerFieldEditPreview CreateResult(
            DimensionTemplateCustomizerFieldEditPreviewState state,
            string code,
            string message,
            bool canApply,
            bool hasWarning,
            string warning,
            string normalizedValue,
            DimensionTemplateCustomizerField field,
            DimensionTemplateCustomizerFieldSet fieldSet)
        {
            return CreateResult(
                state,
                code,
                message,
                canApply,
                hasWarning,
                warning,
                normalizedValue,
                field,
                fieldSet,
                null);
        }

        private static DimensionTemplateCustomizerFieldEditPreview CreateResult(
            DimensionTemplateCustomizerFieldEditPreviewState state,
            string code,
            string message,
            bool canApply,
            bool hasWarning,
            string warning,
            string normalizedValue,
            DimensionTemplateCustomizerField field,
            DimensionTemplateCustomizerFieldSet fieldSet,
            DimensionTemplateCustomizerFieldEditPreview parsedPreview)
        {
            DimensionBounds bounds = parsedPreview == null
                ? new DimensionBounds(new int2(0, 0), new int2(0, 0))
                : parsedPreview.ParsedBounds;
            int parsedInteger = parsedPreview == null ? 0 : parsedPreview.ParsedInteger;
            bool parsedBoolean = parsedPreview != null && parsedPreview.ParsedBoolean;

            return new DimensionTemplateCustomizerFieldEditPreview(
                state,
                code,
                message,
                canApply,
                hasWarning,
                warning,
                normalizedValue,
                bounds,
                parsedPreview != null && parsedPreview.HasParsedBounds,
                parsedInteger,
                parsedPreview != null && parsedPreview.HasParsedInteger,
                parsedBoolean,
                parsedPreview != null && parsedPreview.HasParsedBoolean,
                field,
                fieldSet);
        }
    }
}
