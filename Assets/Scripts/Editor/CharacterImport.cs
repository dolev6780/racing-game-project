using UnityEditor;

namespace RacingGame.EditorTools
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
        // the new look's cut-outs: the hanging leaves and the grass lip along the ground's edge
        const string Canopy = Folder + "Jungle/Backdrop/Canopy", Lip = Folder + "Jungle/Ground/GroundLip",
            Clump = Folder + "Jungle/Backdrop/BushClump", WaterSide = Folder + "Jungle/Water/WaterSide";

        public override uint GetVersion() => 5;

        void OnPreprocessTexture()
        {
            // the cut-out grass cards and painted jungle layers: their mipmaps keep the painted shape's coverage, so they
            // don't thin out and vanish in the distance; a card doesn't repeat (its base would show at its top)
            if (assetPath.StartsWith(Cards) || assetPath.StartsWith(Layers) || assetPath.StartsWith(Canopy) || assetPath.StartsWith(Lip)
                || assetPath.StartsWith(Clump) || assetPath.StartsWith(WaterSide))
            {
                var cut = (TextureImporter)assetImporter;
                cut.mipMapsPreserveCoverage = true;
                cut.alphaTestReferenceValue = 0.5f;
                if (assetPath.StartsWith(Cards)) cut.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                return;
            }
            if (!assetPath.StartsWith(Story)) return;
            var tex = (TextureImporter)assetImporter;
            tex.npotScale = TextureImporterNPOTScale.None;
            tex.mipmapEnabled = false;
            tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            tex.maxTextureSize = 2048;
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
        }
    }
}
