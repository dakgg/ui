using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ViewSystem
{
    [DefaultExecutionOrder(-1000)]
    public class ViewRootCanvas : MonoBehaviour
    {
        public static Transform TempParentTransform => s_GlobalInstance.transform;

        static Canvas s_SceneInstance;
        static Canvas s_GlobalInstance;

        [SerializeField]
        string m_RootViewName;

        void Start()
        {
            //return if no root page
            if (string.IsNullOrWhiteSpace(m_RootViewName)) return;
            ViewRequest.Open($"{m_RootViewName}@View", null, true);
        }

        public static Transform GetSceneRoot()
        {
            if (s_SceneInstance == null)
            {
                var sRoot = Instantiate(Resources.Load<GameObject>("ViewRootCanvas"));
                var baseScene = SceneManager.GetSceneAt(0);
                if (sRoot.scene != baseScene) SceneManager.MoveGameObjectToScene(sRoot, baseScene);
                s_SceneInstance = sRoot.GetComponent<Canvas>();
            }
            return s_SceneInstance.transform;
        }

        public static Transform GetGlobalRoot()
        {
            if (s_GlobalInstance == null)
            {
                var gRoot = Instantiate(Resources.Load<GameObject>("ViewRootCanvas"));
                DontDestroyOnLoad(gRoot);
                s_GlobalInstance = gRoot.GetComponent<Canvas>();
            }
            return s_GlobalInstance.transform;
        }
    }
}