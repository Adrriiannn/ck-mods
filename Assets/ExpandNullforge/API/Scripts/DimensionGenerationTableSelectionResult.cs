using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableSelectionResult
    {
        public readonly bool Success;
        public readonly DimensionGenerationTableResolutionResult Resolution;
        public readonly IReadOnlyList<DimensionGenerationTableSelection> Selections;
        public readonly int SelectionCount;
        public readonly string Code;
        public readonly string Message;

        public DimensionGenerationTableSelectionResult(
            bool success,
            DimensionGenerationTableResolutionResult resolution,
            IReadOnlyList<DimensionGenerationTableSelection> selections,
            int selectionCount,
            string code,
            string message)
        {
            Success = success;
            Resolution = resolution;
            Selections = selections;
            SelectionCount = selectionCount;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
