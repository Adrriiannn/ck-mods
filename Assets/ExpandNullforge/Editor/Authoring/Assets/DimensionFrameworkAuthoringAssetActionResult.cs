using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionFrameworkAuthoringAssetActionResult
    {
        public DimensionFrameworkAuthoringAssetActionResult(
            bool executed,
            Object createdObject,
            string message)
        {
            Executed = executed;
            CreatedObject = createdObject;
            Message = message ?? string.Empty;
        }

        public bool Executed { get; private set; }

        public Object CreatedObject { get; private set; }

        public string Message { get; private set; }
    }
}
