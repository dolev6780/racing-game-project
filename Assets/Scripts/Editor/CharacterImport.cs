using UnityEditor;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Import settings for the modelled Cloudborne characters (Resources/Cloudborne/...): legacy animation, since the
    /// game picks each clip and the moment in it itself (no Animator Controller), and no imported materials, since
    /// the game gives them its own toon material.
    /// </summary>
    public class CharacterImport : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Cloudborne/";

        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var model = (ModelImporter)assetImporter;
            model.animationType = ModelImporterAnimationType.Legacy;
            model.importAnimation = true;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importCameras = false;
            model.importLights = false;
        }
    }
}
