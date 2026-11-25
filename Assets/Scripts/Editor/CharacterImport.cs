using UnityEditor;

namespace Cloudborne.EditorTools
{
    /// <summary>
    /// Import settings for the modelled Cloudborne characters (Resources/Cloudborne/...): legacy animation, since the
    /// game picks each clip and the moment in it itself (no Animator Controller), and no imported materials, since
    /// the game gives them its own toon material. The jungle's scenery stays readable, because the game merges
    /// its copies into big meshes while it runs. The story's paintings (Resources/Cloudborne/Story, the drawn
    /// cutscenes) are shown whole on the screen: kept at their own size (not stretched to a power of two), no
    /// mipmaps, clamped at the edges.
    /// </summary>
    public class CharacterImport : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Cloudborne/";
        const string Scenery = Folder + "Jungle/";
        const string Story = Folder + "Story/";

        const string Cards = Folder + "Jungle/Cards/", Layers = Folder + "Jungle/Backdrop/Jungle";
        // the butterflies and birds (CbCritters), cut out like the cards
        const string Critters = Folder + "Jungle/Critters/";
        // the new look's cut-outs: the hanging leaves and the grass lip along the ground's edge
        const string Lip = Folder + "Jungle/Ground/GroundLip",
            Clump = Folder + "Jungle/Backdrop/BushClump", WaterSide = Folder + "Jungle/Water/WaterSide";

        public override uint GetVersion() => 8;

        void OnPreprocessTexture()
        {
            AndroidCompression();
            // the cut-out grass cards and painted jungle layers: their mipmaps keep the painted shape's coverage, so they
            // don't thin out and vanish in the distance; a card doesn't repeat (its base would show at its top)
            if (assetPath.StartsWith(Cards) || assetPath.StartsWith(Layers) || assetPath.StartsWith(Lip)
                || assetPath.StartsWith(Clump) || assetPath.StartsWith(WaterSide) || assetPath.StartsWith(Critters))
            {
                var cut = (TextureImporter)assetImporter;
                cut.mipMapsPreserveCoverage = true;
                cut.alphaTestReferenceValue = 0.5f;
                if (assetPath.StartsWith(Cards) || assetPath.StartsWith(Critters)) cut.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                return;
            }
            if (!assetPath.StartsWith(Story)) return;
            var tex = (TextureImporter)assetImporter;
            tex.npotScale = TextureImporterNPOTScale.None;
            tex.mipmapEnabled = false;
            tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            tex.maxTextureSize = 2048;
        }

        // the characters, whom the camera follows close: their faces and stripes as sharp as can be
        static readonly string[] Characters = { "Raja/", "Koda/", "Monkey/", "Baba/", "HyenaGuard/", "HyenaRaider/", "HyenaSergeant/", "Ember/Grak" };
        // far behind the path (the painted layers, the trees): the most squeezed
        static readonly string[] Far = { "Jungle/Tree/", "Jungle/Banyan/", "Jungle/Kapok/", "Jungle/Palm/", "Jungle/Karst/", "Jungle/Bamboo/" };

        /// <summary>
        /// The phone's textures (Android): ASTC, as finely as each is seen - the characters 4x4, the painted
        /// backdrops and story pictures 5x5 (they fill the screen), the cut-out cards and HUD 4x4 (their edges), the
        /// models by the path 6x6, the trees far behind it 8x8 at half size. Made smaller and lighter than the
        /// defaults where they are only seen small, kept sharp where they are seen close (the user: top quality, and
        /// it must run well on the phone).
        /// </summary>
        void AndroidCompression()
        {
            if (!assetPath.StartsWith(Folder)) return;
            string rel = assetPath.Substring(Folder.Length);
            var format = TextureImporterFormat.ASTC_6x6;
            int max = 2048;
            if (System.Array.Exists(Characters, c => rel.StartsWith(c))) format = TextureImporterFormat.ASTC_4x4;
            else if (assetPath.StartsWith(Story) || rel.StartsWith("Jungle/Backdrop/") || rel.Contains("Backdrop")) format = TextureImporterFormat.ASTC_5x5;
            else if (assetPath.StartsWith(Cards) || assetPath.StartsWith(Critters) || rel.StartsWith("Hud/") || rel.Contains("Lip") || rel.Contains("Clump")) format = TextureImporterFormat.ASTC_4x4;
            else if (System.Array.Exists(Far, c => rel.StartsWith(c))) { format = TextureImporterFormat.ASTC_8x8; max = 1024; }
            var tex = (TextureImporter)assetImporter;
            var android = tex.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = format;
            android.maxTextureSize = max;
            android.compressionQuality = 100;
            tex.SetPlatformTextureSettings(android);
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var model = (ModelImporter)assetImporter;
            model.animationType = ModelImporterAnimationType.Legacy;
            model.importAnimation = true;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importCameras = false;
            model.importLights = false;
            model.isReadable = assetPath.StartsWith(Scenery);
            // a file of clips only (NAME_Clips.fbx: a skeleton and its clips, made later for a character already in the
            // game) has one root, its skeleton: kept as a child of the file's own root, as in the character's file, so
            // its clips animate the same paths. Unity otherwise makes that skeleton the root, and the clips moved and
            // shrank the whole character instead (Raja vanished: the user).
            typeof(ModelImporter).GetProperty("preserveHierarchy")?.SetValue(model, assetPath.EndsWith("_Clips.fbx"));
        }
    }
}
