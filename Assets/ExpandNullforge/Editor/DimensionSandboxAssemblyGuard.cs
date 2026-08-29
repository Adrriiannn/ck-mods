using System;
using System.Collections.Generic;
using System.Text;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Reads a BUILT assembly and reports every reference in it that Core Keeper's mod sandbox
    /// denies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS ALONGSIDE THE TEXT SCANNER. <see cref="DimensionSandboxGuard"/> reads source
    /// text, and text can hide a reference in ways that have nothing to do with intent: a name split
    /// across two lines, a <c>using</c> alias, a name that only a source generator ever writes down.
    /// The sandbox does not read text. It walks the compiled assembly's reference tables, so a
    /// scanner that walks the same tables sees what the sandbox sees.
    /// </para>
    /// <para>
    /// WHAT IT READS. The ECMA-335 metadata tables of the DLL: every <c>TypeRef</c> — the row the
    /// compiler emits for each type that came from another assembly, however the source spelt it —
    /// every <c>MemberRef</c> with its declaring type, every <c>TypeDef</c> namespace, and whether
    /// the <c>ImplMap</c> table, which is the P/Invoke table, has any rows at all. Generic
    /// instantiations, arrays, by-ref types, locals, catch clauses and <c>typeof</c> all resolve
    /// through TypeRef, so all of them are covered without decoding a single signature blob.
    /// </para>
    /// <para>
    /// IT IS STRICTER THAN THE GAME IN ONE PLACE, deliberately. Custom attributes are in the
    /// reference tables and the game's own checker never looks at them, so a denied type used only
    /// as an attribute is reported here and would in fact load. Nothing in this framework does that,
    /// and being stricter than a bug upstream costs nothing.
    /// </para>
    /// <para>
    /// IT READS FEWER MemberRef ROWS THAN THE TABLE HOLDS, and that is measured rather than
    /// assumed: against <c>System.Reflection.Metadata</c> on the built runtime assembly, TypeRef
    /// came out 906 of 906 and TypeDef 940 of 940 exactly, and MemberRef 2,240 of 4,613. The rows
    /// that go unread are the ones whose parent is not a TypeRef — <c>MethodDef</c>,
    /// <c>ModuleRef</c> and <c>TypeSpec</c> parents, which this reader steps over rather than
    /// resolve. That reaches only the two <c>member:</c> rules in the deny list, and the transcript
    /// records those as inert in the shipped game. The namespace and type rules — every rule that
    /// has ever refused a build — come off TypeRef and TypeDef, which are complete. Anyone about to
    /// lean on a <c>member:</c> rule should read this paragraph first.
    /// </para>
    /// <para>
    /// IT CANNOT SEE ONE THING THE TEXT SCANNER CAN: <c>unsafe</c>. Pointer types live in signature
    /// blobs this does not decode. Both shipping asmdefs set <c>allowUnsafeCode: false</c>, so Unity
    /// refuses to compile it at all, and the text scanner still looks for the keywords.
    /// </para>
    /// <para>
    /// This file is Editor-only and never ships, so it may read files and pick bytes apart freely.
    /// </para>
    /// </remarks>
    public static class DimensionSandboxAssemblyGuard
    {
        /// <summary>Where Unity leaves the assemblies it compiled, relative to the project folder.</summary>
        public const string BuiltAssemblyFolder = "Library/ScriptAssemblies";

        /// <summary>The assemblies that are loaded by the game and therefore security-checked.</summary>
        public static readonly string[] ShippedAssemblies =
        {
            "ExpandNullforge.dll",
            "ExpandNullforge.API.dll",
        };

        /// <summary>Reads one built assembly and returns every denied reference in it.</summary>
        /// <remarks>
        /// A file that cannot be read, or is not a managed assembly, comes back as a finding rather
        /// than as silence, for the same reason a missing deny list does.
        /// </remarks>
        public static List<DimensionSandboxGuard.Finding> ScanAssembly(
            string assemblyPath,
            DimensionSandboxGuard.DenyList denyList)
        {
            List<DimensionSandboxGuard.Finding> findings = new List<DimensionSandboxGuard.Finding>();
            if (denyList == null)
            {
                return findings;
            }

            byte[] image;
            try
            {
                image = System.IO.File.ReadAllBytes(assemblyPath);
            }
            catch (Exception exception)
            {
                findings.Add(new DimensionSandboxGuard.Finding(
                    assemblyPath, 0, "the assembly itself", "Could not be read: " + exception.Message));
                return findings;
            }

            Image reader;
            string why;
            if (!Image.TryOpen(image, out reader, out why))
            {
                findings.Add(new DimensionSandboxGuard.Finding(assemblyPath, 0, "the assembly itself", why));
                return findings;
            }

            List<string> referenced = reader.ReferencedTypeNames();
            for (int i = 0; i < referenced.Count; i++)
            {
                if (IsCompilerEmitted(referenced[i], denyList))
                {
                    continue;
                }

                CheckNamespace(assemblyPath, referenced[i], denyList, findings);
                CheckType(assemblyPath, referenced[i], denyList, findings);
            }

            List<string> defined = reader.DefinedTypeNames();
            for (int i = 0; i < defined.Count; i++)
            {
                CheckNamespace(assemblyPath, defined[i], denyList, findings);
            }

            List<string> members = reader.ReferencedMemberNames();
            for (int i = 0; i < members.Count; i++)
            {
                for (int n = 0; n < denyList.Members.Count; n++)
                {
                    if (string.Equals(members[i], denyList.Members[n], StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(new DimensionSandboxGuard.Finding(
                            assemblyPath, 0, denyList.Members[n], "calls " + members[i]));
                    }
                }
            }

            if (!denyList.AllowPInvoke && reader.PInvokeCount > 0)
            {
                findings.Add(new DimensionSandboxGuard.Finding(
                    assemblyPath,
                    0,
                    "allowPInvoke = false",
                    reader.PInvokeCount + " P/Invoke declaration(s) in the built assembly"));
            }

            return findings;
        }

        /// <summary>
        /// Whether a referenced type is one the C# compiler writes into every assembly on its own.
        /// </summary>
        /// <remarks>
        /// The list is in the transcript beside the docs, one entry per name, each with a note
        /// saying where it was measured in a built assembly and why the game's own checker does not
        /// reach it. Nothing is matched by pattern, so a type that merely LOOKS compiler-emitted is
        /// still reported and a person decides.
        /// </remarks>
        private static bool IsCompilerEmitted(string typeName, DimensionSandboxGuard.DenyList denyList)
        {
            for (int i = 0; i < denyList.CompilerEmitted.Count; i++)
            {
                if (string.Equals(typeName, denyList.CompilerEmitted[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckType(
            string path,
            string typeName,
            DimensionSandboxGuard.DenyList denyList,
            List<DimensionSandboxGuard.Finding> findings)
        {
            for (int i = 0; i < denyList.Types.Count; i++)
            {
                if (string.Equals(typeName, denyList.Types[i], StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new DimensionSandboxGuard.Finding(
                        path, 0, denyList.Types[i], "references " + typeName));
                }
            }
        }

        private static void CheckNamespace(
            string path,
            string typeName,
            DimensionSandboxGuard.DenyList denyList,
            List<DimensionSandboxGuard.Finding> findings)
        {
            for (int i = 0; i < denyList.Namespaces.Count; i++)
            {
                string banned = denyList.Namespaces[i];
                if (typeName.Length > banned.Length &&
                    typeName[banned.Length] == '.' &&
                    typeName.StartsWith(banned, StringComparison.Ordinal))
                {
                    findings.Add(new DimensionSandboxGuard.Finding(
                        path, 0, banned + ".*", "references " + typeName));
                }
            }
        }

        /// <summary>
        /// Just enough of ECMA-335 to list an assembly's referenced type and member names.
        /// </summary>
        /// <remarks>
        /// Only the tables up to <c>MemberRef</c> are laid out, because those are the only ones
        /// read. Every earlier table still has to be sized correctly to find where the later ones
        /// begin, which is all the row-size table below is for.
        /// </remarks>
        private sealed class Image
        {
            private byte[] bytes;
            private int stringsHeap;
            private readonly int[] tableRows = new int[64];
            private readonly int[] tableStart = new int[64];
            private readonly int[] tableRowSize = new int[64];
            private int stringIndexSize;
            private int guidIndexSize;
            private int blobIndexSize;

            /// <summary>How many P/Invoke declarations the assembly carries.</summary>
            public int PInvokeCount { get; private set; }

            public static bool TryOpen(byte[] image, out Image reader, out string why)
            {
                reader = null;
                why = null;
                try
                {
                    Image built = new Image();
                    if (!built.Parse(image, out why))
                    {
                        return false;
                    }

                    reader = built;
                    return true;
                }
                catch (Exception exception)
                {
                    why = "Not a readable managed assembly: " + exception.Message;
                    return false;
                }
            }

            private bool Parse(byte[] image, out string why)
            {
                bytes = image;
                why = null;

                if (bytes.Length < 0x40 || bytes[0] != 0x4D || bytes[1] != 0x5A)
                {
                    why = "Not a PE file.";
                    return false;
                }

                int pe = ReadInt32(0x3C);
                if (pe <= 0 || pe + 24 > bytes.Length || bytes[pe] != 0x50 || bytes[pe + 1] != 0x45)
                {
                    why = "Not a PE file.";
                    return false;
                }

                int sectionCount = ReadUInt16(pe + 6);
                int optionalSize = ReadUInt16(pe + 20);
                int optional = pe + 24;
                int magic = ReadUInt16(optional);
                int directories = optional + (magic == 0x20B ? 112 : 96);

                int sections = optional + optionalSize;
                int[] sectionRva = new int[sectionCount];
                int[] sectionSize = new int[sectionCount];
                int[] sectionRaw = new int[sectionCount];
                for (int i = 0; i < sectionCount; i++)
                {
                    int at = sections + (i * 40);
                    sectionSize[i] = ReadInt32(at + 8);
                    sectionRva[i] = ReadInt32(at + 12);
                    sectionRaw[i] = ReadInt32(at + 20);
                }

                int cliRva = ReadInt32(directories + (14 * 8));
                if (cliRva == 0)
                {
                    why = "No CLI header: this is not a managed assembly.";
                    return false;
                }

                int cli = ToOffset(cliRva, sectionCount, sectionRva, sectionSize, sectionRaw);
                int metadata = ToOffset(
                    ReadInt32(cli + 8), sectionCount, sectionRva, sectionSize, sectionRaw);

                if (ReadInt32(metadata) != 0x424A5342)
                {
                    why = "No metadata root.";
                    return false;
                }

                int versionLength = ReadInt32(metadata + 12);
                int streamCountAt = metadata + 16 + ((versionLength + 3) & ~3) + 2;
                int streamCount = ReadUInt16(streamCountAt);
                int at2 = streamCountAt + 2;

                int tablesStream = -1;
                for (int i = 0; i < streamCount; i++)
                {
                    int offset = ReadInt32(at2);
                    at2 += 8;
                    StringBuilder name = new StringBuilder();
                    while (bytes[at2] != 0)
                    {
                        name.Append((char)bytes[at2]);
                        at2++;
                    }

                    at2 = metadata + (((at2 - metadata) + 4) & ~3);

                    string streamName = name.ToString();
                    if (streamName == "#Strings")
                    {
                        stringsHeap = metadata + offset;
                    }
                    else if (streamName == "#~" || streamName == "#-")
                    {
                        tablesStream = metadata + offset;
                    }
                }

                if (tablesStream < 0 || stringsHeap == 0)
                {
                    why = "The assembly has no table stream or no string heap.";
                    return false;
                }

                int heapSizes = bytes[tablesStream + 6];
                stringIndexSize = (heapSizes & 1) != 0 ? 4 : 2;
                guidIndexSize = (heapSizes & 2) != 0 ? 4 : 2;
                blobIndexSize = (heapSizes & 4) != 0 ? 4 : 2;

                ulong valid = ReadUInt64(tablesStream + 8);
                int rowsAt = tablesStream + 24;
                for (int table = 0; table < 64; table++)
                {
                    if (((valid >> table) & 1UL) != 0UL)
                    {
                        tableRows[table] = ReadInt32(rowsAt);
                        rowsAt += 4;
                    }
                }

                PInvokeCount = tableRows[0x1C];

                int cursor = rowsAt;
                for (int table = 0; table < 64; table++)
                {
                    tableStart[table] = cursor;
                    tableRowSize[table] = RowSize(table);
                    if (tableRows[table] > 0 && tableRowSize[table] == 0)
                    {
                        // A table whose shape this reader does not know, with rows in it. Every
                        // table after it would then be read at the wrong offset, so stop here
                        // rather than report nonsense. Nothing past MemberRef is ever read.
                        break;
                    }

                    cursor += tableRows[table] * tableRowSize[table];
                }

                return true;
            }

            /// <summary>Every type this assembly references from another assembly, by full name.</summary>
            public List<string> ReferencedTypeNames()
            {
                List<string> names = new List<string>();
                for (int row = 1; row <= tableRows[0x01]; row++)
                {
                    names.Add(TypeRefName(row, 0));
                }

                return names;
            }

            /// <summary>Every type this assembly declares, by full name.</summary>
            public List<string> DefinedTypeNames()
            {
                List<string> names = new List<string>();
                int size = tableRowSize[0x02];
                for (int row = 1; row <= tableRows[0x02]; row++)
                {
                    int at = tableStart[0x02] + ((row - 1) * size);
                    string name = ReadString(at + 4);
                    string space = ReadString(at + 4 + stringIndexSize);
                    names.Add(space.Length == 0 ? name : space + "." + name);
                }

                return names;
            }

            /// <summary>Every member this assembly calls on a type from another assembly.</summary>
            public List<string> ReferencedMemberNames()
            {
                List<string> names = new List<string>();
                int size = tableRowSize[0x0A];
                int parentSize = CodedIndexSize(3, MemberRefParent);
                for (int row = 1; row <= tableRows[0x0A]; row++)
                {
                    int at = tableStart[0x0A] + ((row - 1) * size);
                    int parent = parentSize == 2 ? ReadUInt16(at) : ReadInt32(at);
                    int tag = parent & 7;
                    int index = parent >> 3;
                    if (tag != 1 || index <= 0 || index > tableRows[0x01])
                    {
                        continue;
                    }

                    names.Add(TypeRefName(index, 0) + "." + ReadString(at + parentSize));
                }

                return names;
            }

            /// <summary>
            /// The full name of one TypeRef row. A nested type's scope is another TypeRef, so the
            /// enclosing name is walked first; the depth limit is there because a malformed file
            /// could point a row back at itself.
            /// </summary>
            private string TypeRefName(int row, int depth)
            {
                if (row <= 0 || row > tableRows[0x01] || depth > 16)
                {
                    return string.Empty;
                }

                int at = tableStart[0x01] + ((row - 1) * tableRowSize[0x01]);
                int scopeSize = CodedIndexSize(2, ResolutionScope);
                int scope = scopeSize == 2 ? ReadUInt16(at) : ReadInt32(at);
                string name = ReadString(at + scopeSize);
                string space = ReadString(at + scopeSize + stringIndexSize);

                if ((scope & 3) == 3)
                {
                    string outer = TypeRefName(scope >> 2, depth + 1);
                    return outer.Length == 0 ? name : outer + "+" + name;
                }

                return space.Length == 0 ? name : space + "." + name;
            }

            private static readonly int[] ResolutionScope = { 0x00, 0x1A, 0x23, 0x01 };
            private static readonly int[] TypeDefOrRef = { 0x02, 0x01, 0x1B };
            private static readonly int[] MemberRefParent = { 0x02, 0x01, 0x1A, 0x06, 0x1B };

            private int RowSize(int table)
            {
                switch (table)
                {
                    case 0x00:
                        return 2 + stringIndexSize + (guidIndexSize * 3);
                    case 0x01:
                        return CodedIndexSize(2, ResolutionScope) + stringIndexSize + stringIndexSize;
                    case 0x02:
                        return 4 + stringIndexSize + stringIndexSize +
                               CodedIndexSize(2, TypeDefOrRef) +
                               SimpleIndexSize(0x04) + SimpleIndexSize(0x06);
                    case 0x03:
                        return SimpleIndexSize(0x04);
                    case 0x04:
                        return 2 + stringIndexSize + blobIndexSize;
                    case 0x05:
                        return SimpleIndexSize(0x06);
                    case 0x06:
                        return 4 + 2 + 2 + stringIndexSize + blobIndexSize + SimpleIndexSize(0x08);
                    case 0x07:
                        return SimpleIndexSize(0x08);
                    case 0x08:
                        return 2 + 2 + stringIndexSize;
                    case 0x09:
                        return SimpleIndexSize(0x02) + CodedIndexSize(2, TypeDefOrRef);
                    case 0x0A:
                        return CodedIndexSize(3, MemberRefParent) + stringIndexSize + blobIndexSize;
                    default:
                        return 0;
                }
            }

            private int SimpleIndexSize(int table)
            {
                return tableRows[table] < 65536 ? 2 : 4;
            }

            private int CodedIndexSize(int tagBits, int[] tables)
            {
                int biggest = 0;
                for (int i = 0; i < tables.Length; i++)
                {
                    if (tableRows[tables[i]] > biggest)
                    {
                        biggest = tableRows[tables[i]];
                    }
                }

                return biggest < (1 << (16 - tagBits)) ? 2 : 4;
            }

            private string ReadString(int at)
            {
                int index = stringIndexSize == 2 ? ReadUInt16(at) : ReadInt32(at);
                int start = stringsHeap + index;
                int end = start;
                while (end < bytes.Length && bytes[end] != 0)
                {
                    end++;
                }

                return Encoding.UTF8.GetString(bytes, start, end - start);
            }

            private static int ToOffset(int rva, int count, int[] rvas, int[] sizes, int[] raws)
            {
                for (int i = 0; i < count; i++)
                {
                    if (rva >= rvas[i] && rva < rvas[i] + sizes[i])
                    {
                        return raws[i] + (rva - rvas[i]);
                    }
                }

                throw new InvalidOperationException("RVA " + rva + " is in no section.");
            }

            private int ReadUInt16(int at)
            {
                return bytes[at] | (bytes[at + 1] << 8);
            }

            private int ReadInt32(int at)
            {
                return bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24);
            }

            private ulong ReadUInt64(int at)
            {
                return (uint)ReadInt32(at) | ((ulong)(uint)ReadInt32(at + 4) << 32);
            }
        }
    }
}
