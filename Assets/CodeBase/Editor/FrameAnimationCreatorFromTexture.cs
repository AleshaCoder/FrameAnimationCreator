using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.IO;

public class FrameAnimationCreatorFromTexture : EditorWindow
{
    private const string Name = "Frame Animation Creator";

    private Texture2D _atlas;
    private int _cellWidth = 854;
    private int _cellHeight = 480;
    private int _framesPerSecond = 15;
    private string _clipName = "AnimationClip";
    private string _clipPath = "Assets/";
    private string _controllerName = "AnimationController";
    private string _controllerPath = "Assets/";
    private string _prefabrPath = "Assets/";

    [MenuItem("Window/" + Name)]
    private static void Init()
    {
        GetWindow<FrameAnimationCreatorFromTexture>(Name);
    }

    private void OnGUI()
    {
        GUILayout.Label(Name, EditorStyles.boldLabel);
        _atlas = EditorGUILayout.ObjectField("Atlas Texture", _atlas, typeof(Texture2D), false) as Texture2D;
        _cellWidth = EditorGUILayout.IntField("Cell Width", _cellWidth);
        _cellHeight = EditorGUILayout.IntField("Cell Height", _cellHeight);

        _framesPerSecond = EditorGUILayout.IntField("Frames Per Second", _framesPerSecond);

        _clipName = EditorGUILayout.TextField("Clip Name", _clipName);
        _clipPath = EditorGUILayout.TextField("Clip Path", _clipPath);
        _controllerName = EditorGUILayout.TextField("Controller Name", _controllerName);
        _controllerPath = EditorGUILayout.TextField("Controller Path", _controllerPath);

        _prefabrPath = EditorGUILayout.TextField("Demo Prefab Path", _prefabrPath);

        if (GUILayout.Button("Slice Atlas and Create Animation"))
        {
            SliceAtlas();
            CreateAnimation();
        }

        if (GUILayout.Button("Create Animation"))
        {
            CreateAnimation();
        }

        if (GUILayout.Button("Slice Atlas"))
        {
            SliceAtlas();
        }
    }

    private void CreateAnimation()
    {
        AnimationClip clip = CreateAnimationClip();
        AnimatorController animController = CreateAnimator(clip);
        GameObject newPrefab = CreatePrefab(animController);

        ShowAnimation(clip, newPrefab);
    }

    private AnimationClip CreateAnimationClip()
    {
        string atlasPath = AssetDatabase.GetAssetPath(_atlas);
        List<Sprite> sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToList();

        AnimationClip clip = new();
        clip.frameRate = _framesPerSecond;

        EditorCurveBinding spriteBinding = new();
        spriteBinding.type = typeof(SpriteRenderer);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";

        ObjectReferenceKeyframe[] spriteKeyFrames = new ObjectReferenceKeyframe[sprites.Count];

        for (int i = 0; i < sprites.Count; i++)
        {
            spriteKeyFrames[i] = new ObjectReferenceKeyframe
            {
                time = i / (float)_framesPerSecond,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, spriteKeyFrames);

        string clipPath = $"{_clipPath}{_clipName}.anim";

        if (Directory.Exists(_clipPath) == false)
            Directory.CreateDirectory(_clipPath);

        AssetDatabase.CreateAsset(clip, clipPath);
        AssetDatabase.SaveAssets();
        return clip;
    }

    private AnimatorController CreateAnimator(AnimationClip clip)
    {
        if (Directory.Exists(_controllerPath) == false)
            Directory.CreateDirectory(_controllerPath);

        AnimatorController animController = AnimatorController.CreateAnimatorControllerAtPath($"{_controllerPath}{_controllerName}.controller");
        AnimatorStateMachine stateMachine = animController.layers[0].stateMachine;

        AnimatorState state = stateMachine.AddState(_clipName);
        state.motion = clip;
        return animController;
    }

    private GameObject CreatePrefab(AnimatorController animController)
    {
        var temp = new GameObject();

        if (Directory.Exists(_prefabrPath) == false)
            Directory.CreateDirectory(_prefabrPath);

        GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(temp, $"{_prefabrPath}/{_atlas.name}Demo.prefab");
        DestroyImmediate(temp);
        newPrefab.AddComponent<SpriteRenderer>();
        Animator animator = newPrefab.AddComponent<Animator>();
        animator.runtimeAnimatorController = animController;
        return newPrefab;
    }

    private void ShowAnimation(AnimationClip clip, GameObject newPrefab)
    {
        Animator animator = InstatiatePrefabAnimator(newPrefab);

        EditorApplication.ExecuteMenuItem("Window/General/Game");

        PlayableGraph playableGraph = PlayableGraph.Create();
        AnimationPlayableOutput playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(playableGraph, clip);

        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(1.0f);
        playableOutput.SetSourcePlayable(playable);
        playableGraph.Play();
    }

    private Animator InstatiatePrefabAnimator(GameObject newPrefab)
    {
        GameObject instatiatedPrefab = PrefabUtility.InstantiatePrefab(newPrefab) as GameObject;
        Animator animator = instatiatedPrefab.GetComponent<Animator>();
        Selection.activeObject = instatiatedPrefab;
        return animator;
    }

    private void SliceAtlas()
    {
        string texturePath = AssetDatabase.GetAssetPath(_atlas);
        TextureImporter importer = InitTextureImportet(texturePath);
        List<SpriteMetaData> spriteMetaDatas = GenerateSpriteMetaDataFromAtlas();

        importer.spritesheet = spriteMetaDatas.ToArray();
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
    }

    private TextureImporter InitTextureImportet(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        return importer;
    }

    private List<SpriteMetaData> GenerateSpriteMetaDataFromAtlas()
    {
        List<SpriteMetaData> spriteMetaDatas = new();

        int numCellsX = _atlas.width / _cellWidth;
        int numCellsY = _atlas.height / _cellHeight;

        int id = 0;

        for (int y = numCellsY - 1; y >= 0; y--)
        {
            for (int x = 0; x < numCellsX; x++)
            {
                int xPos = x * _cellWidth;
                int yPos = y * _cellHeight;

                SpriteMetaData metaData = new();
                metaData.name = $"{_atlas.name}_{id}";
                id++;
                metaData.rect = new Rect(xPos, yPos, _cellWidth, _cellHeight);
                metaData.alignment = 9;
                metaData.pivot = new Vector2(0.5f, 0.5f);
                spriteMetaDatas.Add(metaData);
            }
        }

        return spriteMetaDatas;
    }
}
