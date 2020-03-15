using UnityEngine;
using Mirror;
using UnityEditor;
using System.Collections;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public class NetworkTest : MonoBehaviour
{
    public NetworkTestScriptableObject prefabs;

    //private const string active_Player = "NetworkTestOnPlayer";
    private const string active_Editor = "NetworkTest";
    private NetworkManager networkManager;

    public static string Active_Editor => Scene + active_Editor;
    public static string Scene => SceneManager.GetActiveScene().name;

    private void Awake()
    {
        // Do not add a NetworkManager, possible error (issue with multiple instances of NetworkManager)
        if (GetComponent<NetworkManager>())
            return;

        // We really don't want NetworkManager
        if (NetworkManager.singleton)
        Destroy(gameObject);

        if (FindObjectOfType<NetworkManager>())
            Destroy(gameObject);

        #if UNITY_EDITOR
        if(prefabs == null)
        {
            prefabs = (NetworkTestScriptableObject)AssetDatabase.LoadAssetAtPath(
                EditorPrefs.GetString(NetworkTestEditor.prefabsAssetPath),
                NetworkTestEditor.NetworkTestPrefabs);

            if (prefabs == null)
            {
                Debug.LogError("Prefabs not found!");
                enabled = false;
            }

        }
        #endif
    }

    void Start()
    {
        networkManager = NetworkManager.singleton;

        // Do not add a NetworkManager, possible error (issue with multiple instances of NetworkManager)
        if (!GetComponent<NetworkManager>())
        {
            // We don't want to execut a test if they already a network instance
            if (networkManager)
                Destroy(gameObject);

            // If not NetworkManager spawn one
            networkManager = gameObject.AddComponent<NetworkManager>();
            // and init
            InitManager();
        }

        // We don't want to execut a test on a player
#if UNITY_EDITOR
        /*// We don't want to execut a test if the value are never created
        if (!EditorPrefs.HasKey(Active_Editor))
            Destroy(gameObject);

        // We don't want to execut a test if the user don't want
        if (!EditorPrefs.GetBool(Active_Editor))
            Destroy(gameObject);*/

        StartNetwork();
#else
        // if it's not the first scene so we probably don't want to test
        if (SceneManager.GetActiveScene().buildIndex != 0)
            Destroy(gameObject);

        StartCoroutine(JoinNetwork());
#endif
    }

    private void InitManager()
    {
        networkManager.playerPrefab = prefabs.playerPrefab;
        //Set to 60 tick rate because a lot of device have 60hz screen
        networkManager.serverTickRate = 60;

        // Auto
        networkManager.playerSpawnMethod = FindObjectOfType<NetworkStartPosition>() ? PlayerSpawnMethod.RoundRobin : PlayerSpawnMethod.Random;

        networkManager.spawnPrefabs = prefabs.spawnPrefabs;

        // Like InitializeSingleton but without a return
        if (!TryGetComponent(out Transport.activeTransport))
        {
            if (!TryGetComponent(out TelepathyTransport telepathy))
                Debug.LogError("Fail init on net test");
            else
                Transport.activeTransport = telepathy;
        }
    }

    void StartNetwork()
    {
        networkManager.StartHost();
    }

    IEnumerator JoinNetwork()
    {
        while (true)
        {
            if (!NetworkClient.active)
                networkManager.StartClient();
            yield return new WaitForSeconds(1);
        }
    }

    private void OnGUI()
    {
        if (NetworkClient.active && !NetworkServer.active)
        {
            if (NetworkClient.isConnected)
                //GUILayout.Label("Client: address=" + networkManager.networkAddress);
                return;
            else
                // Connecting
                GUILayout.Label("Connecting to " + networkManager.networkAddress + "..");
        }
    }
}

#if UNITY_EDITOR
public class NetworkTestEditor : EditorWindow
{
    [MenuItem("Multiplayer/Mirror")]

    public static void ShowWindow() => GetWindow(typeof(NetworkTestEditor));

    bool Active
    {
        get => EditorPrefs.GetBool(NetworkTest.Active_Editor);
        set => EditorPrefs.SetBool(NetworkTest.Active_Editor, value);
    }

    NetworkTestScriptableObject prefabs;
    public const string prefabsAssetPath = "NetworkTestPrefabsPath";
    public const string defaultPrefabsAssetPath = "Assets/NetworkTestPrefabs.asset";
    public static readonly Type NetworkTestPrefabs = typeof(NetworkTestScriptableObject);

    bool spawnPrefabs;

    void OnGUI()
    {
        GUILayout.Space(5);
        GUILayout.Label("NetworkTestPrefabs.asset must be at the root of \nthe assets folder and not be renamed.", EditorStyles.miniLabel);
        GUILayout.Space(5);
        GUILayout.Label("Prefabs:", EditorStyles.boldLabel);

        // if prefabs null
        if (prefabs == null)
            if (EditorPrefs.HasKey(prefabsAssetPath))
            {
                prefabs = (NetworkTestScriptableObject)AssetDatabase.LoadAssetAtPath(
                    EditorPrefs.GetString(prefabsAssetPath),
                    NetworkTestPrefabs);

                // if deleted
                if (prefabs == null)
                    FindPrefabOrCreate();
            }
            else
                // if not find
                FindPrefabOrCreate();

        if (prefabs == null)    // That normally never append
            return;

        // Player prefabs var
        prefabs.playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player prefabs", prefabs.playerPrefab, typeof(GameObject), false);

        // Spawn prefabs var
        if (spawnPrefabs = EditorGUILayout.BeginFoldoutHeaderGroup(spawnPrefabs, "Spawn prefabs", EditorStyles.foldout))
        {
            for (int i = 0; i < prefabs.spawnPrefabs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                prefabs.spawnPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs.spawnPrefabs[i], typeof(GameObject), false);
                if (GUILayout.Button("Remove"))
                    prefabs.spawnPrefabs.Remove(prefabs.spawnPrefabs[i]);

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add"))
                prefabs.spawnPrefabs.Add(null);

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        GUILayout.Space(5);

        // Host when....
        GUILayout.Label($"Host ({NetworkTest.Scene}) when:");

        NetworkTest networkTest = FindObjectOfType<NetworkTest>();

        // if manually removed;
        Active = networkTest;

        Active = GUILayout.Toggle(Active, "Enter Play Mode");

        // Apply settings
        if (Active)
        {
            if (!networkTest)
            {
                GameObject go = new GameObject(nameof(NetworkTest), typeof(NetworkTest));
                Undo.RecordObjects(new UnityEngine.Object[] { go, this}, "Add GameObject");
                go.GetComponent<NetworkTest>().prefabs = prefabs;
            }
        } else
        {
            if (networkTest)
            {
                Undo.RecordObjects(new UnityEngine.Object[] { networkTest.gameObject, this }, "Remove GameObject");
                DestroyImmediate(networkTest.gameObject);
            }
        }
    }

    void FindPrefabOrCreate()
    {
        string[] path = AssetDatabase.FindAssets("t:NetworkTestScriptableObject");
        if (path.Length < 0)
            prefabs = (NetworkTestScriptableObject)AssetDatabase.LoadAssetAtPath(
                path[0],    //We take the first beacause we want only one
                NetworkTestPrefabs);
        // if we don't have one so we create one
        else
        {
            prefabs = CreateInstance<NetworkTestScriptableObject>();
            AssetDatabase.CreateAsset(prefabs, defaultPrefabsAssetPath);

            EditorPrefs.SetString(prefabsAssetPath, defaultPrefabsAssetPath);
        }
    }
}
#endif

[Serializable]
// [CreateAssetMenu(fileName = "NetworkTest", menuName = "Multiplayer/Mirror", order = 1)]
public class NetworkTestScriptableObject : ScriptableObject
{
    public GameObject playerPrefab;
    public List<GameObject> spawnPrefabs = new List<GameObject>();
}