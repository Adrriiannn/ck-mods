using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What a crop looks like in the ground, stage by stage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE PICTURE PER STAGE, INCLUDING THE RIPE ONE. Core Keeper counts a plant's stages from zero
    /// up to and including its top stage, so a crop with two growth stages passes through three
    /// looks: just sprouted, half grown, ripe. Drawing one fewer leaves the last stage with nothing
    /// to show, which is why the generator refuses to be vague about the count.
    /// </para>
    /// <para>
    /// A PICTURE IS ONE ROW, ALWAYS. The game slices a row by width only — picture n is the nth
    /// slice of <c>width / count</c> pixels. A sheet stacked in rows cannot be read at all and
    /// there is no setting that changes that. Most crop stages are a single picture; the ripe one
    /// usually waves, which is a row of several.
    /// </para>
    /// <para>
    /// WHY THE COUNTS ARE ASKED FOR RATHER THAN MEASURED. Creature strips are square-ish, so their
    /// picture count can be guessed from the shape of the file. Crop art is not: a ripe plant two
    /// tiles tall drawn eight times across is wider than it is tall by four, not by eight, and a
    /// guess would silently play the wrong half of every picture.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionPlantArtTemplate
    {
        /// <summary>Pictures per second a row plays at, matching every vanilla crop.</summary>
        public const float VanillaSpeed = 10f;

        /// <summary>How many pictures vanilla's twinkle is drawn across.</summary>
        public const int VanillaShinePictures = 4;

        [Tooltip("One picture for each stage the plant passes through, from just sprouted to ripe. A crop with two growth stages needs three.")]
        [SerializeField] private Texture2D[] growthPictures = new Texture2D[0];

        [Tooltip("How many pictures are side by side in each of those rows. Leave empty for one each. The game's own crops wave through eight when ripe.")]
        [SerializeField] private int[] picturesInEachRow = new int[0];

        [Tooltip("The twinkle that runs over a ripe plant every few seconds. Optional.")]
        [SerializeField] private Texture2D shinePicture;

        [Tooltip("How many pictures the twinkle is drawn across.")]
        [Min(1)]
        [SerializeField] private int picturesInTheShineRow = VanillaShinePictures;

        [Tooltip("Pictures per second for any row with more than one picture.")]
        [Min(0f)]
        [SerializeField] private float pictureSpeed = VanillaSpeed;

        [Tooltip("The seed sitting in the soil, before it sprouts. Not the inventory icon.")]
        [SerializeField] private Texture2D seedPicture;

        [Tooltip("The same seed once the soil around it has been watered. Optional.")]
        [SerializeField] private Texture2D seedInWetGroundPicture;

        [Tooltip("Whether it lays a shadow of itself on the ground, the way every crop in the game does.")]
        [SerializeField] private bool castsAShadow = true;

        [Tooltip("The light the plant itself gives off. Black for a plant that does not glow.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color glowColour = Color.black;

        [Tooltip("The light it throws onto the ground around it. Black for none.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color groundGlowColour = Color.black;

        [Tooltip("Only glow once it is ripe, the way the game's glow tulips do. Turn off to glow at every stage.")]
        [SerializeField] private bool glowsOnlyWhenRipe = true;

        [Tooltip("Draw it at full brightness whatever the light in the cave. For plants that are their own light source.")]
        [SerializeField] private bool ignoresTorchlight;

        [Tooltip("The sound it makes the moment it ripens. A sound name; leave empty for silence.")]
        [DimensionSoundName]
        [SerializeField] private string ripeSoundId = string.Empty;

        [Tooltip("The little burst of bits thrown up when it ripens. A PuffID name; leave empty for leaves.")]
        [DimensionPuffName]
        [SerializeField] private string ripePuffId = string.Empty;

        /// <summary>The stage pictures, never null.</summary>
        public Texture2D[] GrowthPictures
        {
            get { return growthPictures ?? new Texture2D[0]; }
        }

        public Texture2D ShinePicture
        {
            get { return shinePicture; }
        }

        public int PicturesInTheShineRow
        {
            get { return picturesInTheShineRow < 1 ? 1 : picturesInTheShineRow; }
        }

        /// <summary>Pictures per second, never zero — a zero divides by nothing at playback.</summary>
        public float PictureSpeed
        {
            get { return pictureSpeed <= 0f ? VanillaSpeed : pictureSpeed; }
        }

        public Texture2D SeedPicture
        {
            get { return seedPicture; }
        }

        public Texture2D SeedInWetGroundPicture
        {
            get { return seedInWetGroundPicture; }
        }

        public bool CastsAShadow
        {
            get { return castsAShadow; }
        }

        public Color GlowColour
        {
            get { return glowColour; }
        }

        public Color GroundGlowColour
        {
            get { return groundGlowColour; }
        }

        public bool GlowsOnlyWhenRipe
        {
            get { return glowsOnlyWhenRipe; }
        }

        public bool IgnoresTorchlight
        {
            get { return ignoresTorchlight; }
        }

        public string RipeSoundId
        {
            get { return ripeSoundId ?? string.Empty; }
        }

        public string RipePuffId
        {
            get { return ripePuffId ?? string.Empty; }
        }

        /// <summary>Whether anything at all was drawn for the plant in the ground.</summary>
        public bool HasPlantPictures
        {
            get
            {
                Texture2D[] pictures = GrowthPictures;
                for (int i = 0; i < pictures.Length; i++)
                {
                    if (pictures[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Whether the seed has anything to show while it sits in the soil.</summary>
        public bool HasSeedPicture
        {
            get { return seedPicture != null; }
        }

        /// <summary>
        /// How many pictures the row for one stage holds.
        /// </summary>
        /// <remarks>
        /// Never guessed from the file's shape. See the class remarks: crop art is regularly taller
        /// than it is wide, so the guess creature art can afford is wrong here often enough to be
        /// worse than asking.
        /// </remarks>
        public int PicturesInRow(int stageIndex)
        {
            if (picturesInEachRow == null ||
                stageIndex < 0 ||
                stageIndex >= picturesInEachRow.Length ||
                picturesInEachRow[stageIndex] < 1)
            {
                return 1;
            }

            return picturesInEachRow[stageIndex];
        }
    }

    /// <summary>
    /// What one better version of a crop looks like — the golden one, and anything rarer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE PART CORE KEEPER ALREADY HAS AND THE FRAMEWORK COULD NOT REACH. A golden crop
    /// is the same object at another variation, and the game picks its art by matching that
    /// variation — which is exactly what the framework's own registry does, one entry per version.
    /// So a version's look costs nothing structurally: it is a second set of pictures under a
    /// second name.
    /// </para>
    /// <para>
    /// EVERYTHING LEFT EMPTY IS THE CROP'S OWN. A version that draws nothing looks exactly like the
    /// ordinary crop, which is legal and almost never what anybody meant, so the generator says so.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCropVersionLookTemplate
    {
        [Tooltip("One picture per stage, the same count as the crop's own. Leave empty to look exactly like the ordinary crop.")]
        [SerializeField] private Texture2D[] growthPictures = new Texture2D[0];

        [Tooltip("The twinkle over this version when ripe. Leave empty to reuse the crop's.")]
        [SerializeField] private Texture2D shinePicture;

        [Tooltip("This version's seed in the soil. Leave empty to reuse the crop's.")]
        [SerializeField] private Texture2D seedPicture;

        [Tooltip("The same seed in watered soil. Leave empty to reuse the crop's.")]
        [SerializeField] private Texture2D seedInWetGroundPicture;

        [Tooltip("A colour wash over the pictures. It multiplies what is already drawn, so it can deepen a colour but never brighten one: for a golden version, draw the pictures golden.")]
        [ColorUsage(false, false)]
        [SerializeField] private Color colourWash = Color.white;

        public Texture2D[] GrowthPictures
        {
            get { return growthPictures ?? new Texture2D[0]; }
        }

        public Texture2D ShinePicture
        {
            get { return shinePicture; }
        }

        public Texture2D SeedPicture
        {
            get { return seedPicture; }
        }

        public Texture2D SeedInWetGroundPicture
        {
            get { return seedInWetGroundPicture; }
        }

        /// <summary>
        /// The wash to multiply the pictures by. White for a version that draws its own colours.
        /// </summary>
        /// <remarks>
        /// FULLY TRANSPARENT BLACK READS AS NO WASH, and that is not a nicety. Unity fills a row
        /// added to an empty list with zeroes rather than running the field initialisers, so the
        /// very first version anybody adds arrives with a wash of (0, 0, 0, 0) — which multiplied
        /// over the crop's pictures is a plant that cannot be seen at all. Nobody has ever wanted
        /// an invisible version, so that exact value means the field was never filled in.
        /// </remarks>
        /// <remarks>
        /// Always fully opaque. Sprite objects are gathered into one enormous unsorted draw call
        /// and only support opaque pictures — the SDK says so on the materials themselves — so a
        /// half-transparent wash would not fade a crop, it would draw it wrongly against whatever
        /// happened to be behind it.
        /// </remarks>
        public Color ColourWash
        {
            get
            {
                if (colourWash.a <= 0f && colourWash.r <= 0f &&
                    colourWash.g <= 0f && colourWash.b <= 0f)
                {
                    return Color.white;
                }

                return new Color(colourWash.r, colourWash.g, colourWash.b, 1f);
            }
        }

        /// <summary>Whether it draws the plant in the ground itself.</summary>
        public bool HasPlantPictures
        {
            get
            {
                Texture2D[] pictures = GrowthPictures;
                for (int i = 0; i < pictures.Length; i++)
                {
                    if (pictures[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasSeedPicture
        {
            get { return seedPicture != null; }
        }

        /// <summary>Whether the wash would change anything at all.</summary>
        public bool WashesColour
        {
            get
            {
                Color wash = ColourWash;
                return wash.r < 1f || wash.g < 1f || wash.b < 1f;
            }
        }

        /// <summary>Whether this version looks any different from the ordinary crop.</summary>
        public bool LooksDifferent
        {
            get { return HasPlantPictures || HasSeedPicture || ShinePicture != null || WashesColour; }
        }
    }
}
