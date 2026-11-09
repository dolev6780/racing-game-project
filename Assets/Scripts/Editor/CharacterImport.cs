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

        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
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
