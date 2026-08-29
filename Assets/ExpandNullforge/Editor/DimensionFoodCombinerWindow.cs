using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Food;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Put two foods in the pot and see what comes out, before anything is built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE POT HAS NO RECIPE LIST, which is why this window is worth having. Every pair of
    /// ingredients is a recipe: one of the two leads, the dish that comes out is whatever that one
    /// names, and both are remembered on the result so it can be tinted and buffed by what went in.
    /// A creator adding an ingredient is therefore adding a couple of hundred dishes at once, and
    /// the only honest way to see what they have made is to work the pair out the same way the
    /// game does.
    /// </para>
    /// <para>
    /// THE PREVIEW IS THE GAME'S OWN ARITHMETIC, not an imitation of it. Both sides call
    /// <see cref="DimensionFoodPairing"/>, which is checked against the game's implementation by a
    /// test, so a pair shown here is a pair the pot agrees with.
    /// </para>
    /// <para>
    /// WHERE IT CANNOT ANSWER, IT SAYS SO. Two of the game's own foods resolve exactly, because
    /// both their numbers are known. Anything of the creator's own does not have a number until the
    /// game hands one out at load, and the tie between two ordinary ingredients is decided from
    /// those numbers — so the answer is one of two dishes, named, rather than a guess dressed as a
    /// fact. It is still the same answer in every world once it is decided.
    /// </para>
    /// </remarks>
    internal sealed class DimensionFoodCombinerWindow : EditorWindow
    {
        private const float SlotWidth = 300f;
        private const int MaxStrangeColoursShown = 6;

        private DimensionTemplateAsset template;
        private Choice first;
        private Choice second;
        private string firstFilter = string.Empty;
        private string secondFilter = string.Empty;
        private Vector2 firstScroll;
        private Vector2 secondScroll;
        private Vector2 pageScroll;
        private string actionMessage = string.Empty;

        /// <summary>One of the two things in the pot: a food of the creator's, or one of the game's.</summary>
        private struct Choice
        {
            public DimensionItemAsset Mine;
            public int GameObjectId;

            public bool IsMine
            {
                get { return Mine != null; }
            }

            public bool IsSomething
            {
                get { return Mine != null || GameObjectId != 0; }
            }
        }

        [MenuItem("Dimensions API/Food Combiner")]
        private static void Open()
        {
            DimensionFoodCombinerWindow window = GetWindow<DimensionFoodCombinerWindow>();
            window.titleContent = new GUIContent("Food Combiner");
            window.minSize = new Vector2(720f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            if (template == null)
            {
                template = FindTheOnlyTemplate();
            }
        }

        private void OnGUI()
        {
            pageScroll = EditorGUILayout.BeginScrollView(pageScroll);

            template = (DimensionTemplateAsset)EditorGUILayout.ObjectField(
                "Dimension Asset", template, typeof(DimensionTemplateAsset), false);

            if (!string.IsNullOrEmpty(actionMessage))
            {
                EditorGUILayout.HelpBox(actionMessage, MessageType.Info);
            }

            EditorGUILayout.LabelField(
                "Pick two foods. Anything that can go in the pot can go here, yours and the " +
                "game's alike.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginHorizontal();
            DrawSlot("First", ref first, ref firstFilter, ref firstScroll);
            DrawSlot("Second", ref second, ref secondFilter, ref secondScroll);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawOutcome();

            GUILayout.Space(12f);
            DrawDishes();

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------ the slots ---

        private void DrawSlot(
            string title,
            ref Choice choice,
            ref string filter,
            ref Vector2 scroll)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(SlotWidth));
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Describe(choice),
                EditorStyles.wordWrappedLabel);

            if (choice.IsMine)
            {
                DrawRamp(choice.Mine);
            }

            filter = EditorGUILayout.TextField("Search", filter);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(190f));
            string needle = (filter ?? string.Empty).Trim();

            List<DimensionItemAsset> mine = MyIngredients();
            if (mine.Count > 0)
            {
                EditorGUILayout.LabelField("Yours", EditorStyles.miniBoldLabel);
                for (int i = 0; i < mine.Count; i++)
                {
                    if (!Matches(mine[i].DisplayName, needle))
                    {
                        continue;
                    }

                    if (GUILayout.Button(mine[i].DisplayName, EditorStyles.miniButton))
                    {
                        choice = new Choice { Mine = mine[i], GameObjectId = 0 };
                    }
                }
            }

            EditorGUILayout.LabelField("The game's", EditorStyles.miniBoldLabel);
            List<int> ids = DimensionFoodCatalog.AllIngredientIdsByName();
            for (int i = 0; i < ids.Count; i++)
            {
                string name = DimensionFoodCatalog.ReadableName(ids[i]);
                if (!Matches(name, needle))
                {
                    continue;
                }

                if (GUILayout.Button(name, EditorStyles.miniButton))
                {
                    choice = new Choice { Mine = null, GameObjectId = ids[i] };
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>The four shades this ingredient will lend a dish, as the generator will read them.</summary>
        private static void DrawRamp(DimensionItemAsset item)
        {
            DimensionCookingTemplate cooking = item.Cooking;
            Color[] shades = null;
            if (cooking.ColoursFromItsOwnPicture && item.IconSprite != null)
            {
                DimensionFoodPalette.TryExtractRamp(item.IconSprite, out shades);
            }

            bool fromPicture = shades != null;
            if (!fromPicture)
            {
                shades = new[] { cooking.Brightest, cooking.Bright, cooking.Dark, cooking.Darkest };
            }

            Rect row = GUILayoutUtility.GetRect(SlotWidth - 16f, 14f);
            float width = row.width / shades.Length;
            for (int i = 0; i < shades.Length; i++)
            {
                EditorGUI.DrawRect(
                    new Rect(row.x + (i * width), row.y, width - 1f, row.height), shades[i]);
            }

            EditorGUILayout.LabelField(
                fromPicture ? "Colours read from its picture" : "Colours typed on the item",
                EditorStyles.miniLabel);
        }

        // ---------------------------------------------------------- the outcome ---

        private void DrawOutcome()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Out of the pot", EditorStyles.boldLabel);

            if (!first.IsSomething || !second.IsSomething)
            {
                EditorGUILayout.LabelField(
                    "Pick a food on each side.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            string firstDish = DishOf(first);
            string secondDish = DishOf(second);

            if (string.IsNullOrEmpty(firstDish) || string.IsNullOrEmpty(secondDish))
            {
                EditorGUILayout.HelpBox(
                    "One of these two makes no dish, so the pot will refuse the pair. Set which " +
                    "dish it makes on the item.",
                    MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            if (!first.IsMine && !second.IsMine)
            {
                DrawCertainOutcome(first.GameObjectId, second.GameObjectId);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawUncertainOutcome(firstDish, secondDish);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Two of the game's own foods: both numbers are known, so the answer is exact.
        /// </summary>
        private void DrawCertainOutcome(int a, int b)
        {
            int primary = DimensionFoodPairing.Primary(a, b);
            int secondary = DimensionFoodPairing.Secondary(a, b);
            int dish = DimensionFoodCatalog.DishFor(primary);

            EditorGUILayout.LabelField(
                DimensionFoodCatalog.ReadableName(dish),
                EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                DimensionFoodCatalog.ReadableName(primary) + " leads, so the dish is its. " +
                DimensionFoodCatalog.ReadableName(secondary) +
                " lends its colours and what it gives you.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "Both are the game's own, so this is already in the game and there is nothing to " +
                "author. The pair is remembered on the dish as " +
                DimensionFoodPairing.Variation(a, b) + ".",
                EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>
        /// At least one food is the creator's, so the tie cannot be settled until the game runs.
        /// </summary>
        /// <remarks>
        /// It is still worth saying plenty. When both foods point at the same dish the answer is
        /// certain even though the lead is not, and when one of them always leads the answer is
        /// certain outright. Only the genuinely open case is printed as two possibilities, and it is
        /// printed as two named dishes rather than a shrug.
        /// </remarks>
        private void DrawUncertainOutcome(string firstDish, string secondDish)
        {
            bool firstAlwaysLeads = !first.IsMine &&
                DimensionFoodPairing.AlwaysLeads(first.GameObjectId);
            bool secondAlwaysLeads = !second.IsMine &&
                DimensionFoodPairing.AlwaysLeads(second.GameObjectId);

            if (firstAlwaysLeads != secondAlwaysLeads)
            {
                string winner = firstAlwaysLeads ? firstDish : secondDish;
                EditorGUILayout.LabelField(DescribeDish(winner), EditorStyles.largeLabel);
                EditorGUILayout.LabelField(
                    Describe(firstAlwaysLeads ? first : second) +
                    " always leads, whatever it is put with, so the dish is certain.",
                    EditorStyles.wordWrappedLabel);
                DrawDishActions(winner);
                return;
            }

            if (string.Equals(firstDish, secondDish, StringComparison.Ordinal))
            {
                EditorGUILayout.LabelField(DescribeDish(firstDish), EditorStyles.largeLabel);
                EditorGUILayout.LabelField(
                    "Both point at the same dish, so it does not matter which of them leads. " +
                    "Which one lends the colours is still decided when the game runs.",
                    EditorStyles.wordWrappedLabel);
                DrawDishActions(firstDish);
                return;
            }

            EditorGUILayout.LabelField(
                DescribeDish(firstDish) + "  or  " + DescribeDish(secondDish),
                EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "One of these two, and the game decides which. It compares the two foods' object " +
                "numbers, and your mod's numbers are handed out when the game loads. Once decided " +
                "it is the same answer in every world, forever — but it cannot be worked out here.",
                MessageType.Info);
            DrawDishActions(firstDish);
            DrawDishActions(secondDish);

            if (first.IsMine || second.IsMine)
            {
                EditorGUILayout.LabelField(
                    "A golden food of your own cannot force itself to lead. The game decides that " +
                    "from a band of its own object numbers, deep inside code no mod reaches. Your " +
                    "golden version still aims at the better dish and still counts towards an epic " +
                    "one; it just leads half the time rather than always.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawDishActions(string dishId)
        {
            DimensionDishAsset mine = FindMyDish(dishId);
            if (mine == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open " + mine.DisplayName, GUILayout.Width(220f)))
            {
                Selection.activeObject = mine;
                EditorGUIUtility.PingObject(mine);
            }

            EditorGUILayout.EndHorizontal();
            DrawDishHealth(mine);
        }

        // ------------------------------------------------------------ the dishes ---

        private void DrawDishes()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Your dishes", EditorStyles.boldLabel);
            if (GUILayout.Button("Add a dish", GUILayout.Width(110f)))
            {
                DimensionFrameworkAuthoringAssetActionResult result =
                    DimensionFrameworkAuthoringAssetUtility.CreateDish(template);
                actionMessage = result.Message;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "A dish is one picture the game recolours from whatever went in, in three " +
                "qualities. Draw it in these colours: " + DimensionFoodPalette.DescribeTemplate(),
                EditorStyles.wordWrappedMiniLabel);

            DimensionDishAsset[] dishes = template == null
                ? new DimensionDishAsset[0]
                : template.GlobalDishes;
            if (dishes.Length == 0)
            {
                EditorGUILayout.LabelField(
                    "No dishes of your own yet. Your ingredients can still make the game's " +
                    "fifteen — add a dish only when you want a new kind of food.",
                    EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < dishes.Length; i++)
            {
                if (dishes[i] == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(dishes[i].DisplayName, EditorStyles.miniButton,
                        GUILayout.Width(220f)))
                {
                    Selection.activeObject = dishes[i];
                    EditorGUIUtility.PingObject(dishes[i]);
                }

                EditorGUILayout.LabelField(dishes[i].DishId, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                DrawDishHealth(dishes[i]);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// What is wrong with a dish's picture, if anything, in the words that name the fix.
        /// </summary>
        /// <remarks>
        /// The palette check is the one thing about a dish that cannot be seen by looking at it in
        /// the editor: a pixel painted off-palette looks perfectly fine here and simply never
        /// changes colour in game, whatever went into the pot.
        /// </remarks>
        private static void DrawDishHealth(DimensionDishAsset dish)
        {
            if (dish.BaseSprite == null)
            {
                EditorGUILayout.HelpBox(
                    dish.DisplayName + " has no picture yet, so it will be invisible in the " +
                    "inventory. Draw one in the eight colours above and drop it on the dish.",
                    MessageType.Warning);
                return;
            }

            List<Color32> strangers = DimensionFoodPalette.FindColoursOutsideTheTemplate(
                dish.BaseSprite, MaxStrangeColoursShown);
            if (strangers.Count == 0)
            {
                return;
            }

            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < strangers.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append('#').Append(ColorUtility.ToHtmlStringRGB(strangers[i]));
            }

            EditorGUILayout.HelpBox(
                dish.DisplayName + " uses colours the game will not recolour: " + text +
                (strangers.Count >= MaxStrangeColoursShown ? " and more" : string.Empty) +
                ". Those pixels stay exactly as drawn whatever goes in the pot, which is right for " +
                "a plate and wrong for the food. Repaint them in the eight colours above.",
                MessageType.Warning);
        }

        // ------------------------------------------------------------- plumbing ---

        private List<DimensionItemAsset> MyIngredients()
        {
            List<DimensionItemAsset> mine = new List<DimensionItemAsset>();
            if (template == null)
            {
                return mine;
            }

            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].Enabled && items[i].Cooking.IsAnIngredient)
                {
                    mine.Add(items[i]);
                }
            }

            return mine;
        }

        /// <summary>The id of the dish a chosen food makes when it leads.</summary>
        private string DishOf(Choice choice)
        {
            if (choice.IsMine)
            {
                return choice.Mine.Cooking.MakesDish;
            }

            int dish = DimensionFoodCatalog.DishFor(choice.GameObjectId);
            return dish == 0 ? string.Empty : ((ObjectID)dish).ToString();
        }

        private static string Describe(Choice choice)
        {
            if (choice.IsMine)
            {
                return choice.Mine.DisplayName + "  (yours)";
            }

            return choice.GameObjectId == 0
                ? "Nothing chosen"
                : DimensionFoodCatalog.ReadableName(choice.GameObjectId);
        }

        /// <summary>A dish id as a creator would read it, saying whose dish it is.</summary>
        private string DescribeDish(string dishId)
        {
            DimensionDishAsset mine = FindMyDish(dishId);
            if (mine != null)
            {
                return mine.DisplayName + " (yours)";
            }

            ObjectID vanilla = DimensionObjectBinder.Vanilla(dishId);
            if (vanilla != ObjectID.None)
            {
                return DimensionFoodCatalog.ReadableName((int)vanilla) + " (the game's)";
            }

            return "nothing — '" + dishId + "' is not a dish";
        }

        /// <summary>
        /// The creator's dish an id names, whichever of its three qualities was named.
        /// </summary>
        private DimensionDishAsset FindMyDish(string dishId)
        {
            if (template == null || string.IsNullOrEmpty(dishId))
            {
                return null;
            }

            DimensionDishAsset[] dishes = template.GlobalDishes;
            for (int i = 0; i < dishes.Length; i++)
            {
                DimensionDishAsset dish = dishes[i];
                if (dish == null)
                {
                    continue;
                }

                if (string.Equals(dish.DishId, dishId, StringComparison.Ordinal) ||
                    string.Equals(dish.RareItemId, dishId, StringComparison.Ordinal) ||
                    string.Equals(dish.EpicItemId, dishId, StringComparison.Ordinal))
                {
                    return dish;
                }
            }

            return null;
        }

        private static bool Matches(string name, string needle)
        {
            return needle.Length == 0 ||
                (name != null &&
                 name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// The dimension this project is about, when there is only one of them.
        /// </summary>
        /// <remarks>
        /// Only picked automatically when the answer is unambiguous. Guessing between two would put
        /// a creator to work on the wrong dimension without ever telling them which one they were
        /// looking at.
        /// </remarks>
        private static DimensionTemplateAsset FindTheOnlyTemplate()
        {
            string[] guids = AssetDatabase.FindAssets("t:DimensionTemplateAsset");
            if (guids == null || guids.Length != 1)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<DimensionTemplateAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
