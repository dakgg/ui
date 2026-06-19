using UnityEngine;

namespace ViewSystem
{
    public interface IViewCamera
    {
        void SetViewCameraDepth(int depth);

        void SetVisibleState(bool visible);
    }
    
    public class CameraPageView<T> : PageView, IViewAddedListener, IViewRemovedListener, IViewVisibleListener
        where T : MonoBehaviour, IViewCamera
    {
        [field: SerializeField] 
        private T m_CameraPrefab;

        public T CameraInstance { get; private set; }
        
        public virtual void OnViewAdded()
        {
            if (m_CameraPrefab != null)
            {
                CameraInstance = Instantiate(m_CameraPrefab);
                CameraInstance.SetViewCameraDepth(GetComponent<Canvas>().sortingOrder);
            }
        }

        public virtual void OnViewRemoved()
        {
            if (CameraInstance != null)
            {
                Destroy(CameraInstance.gameObject);
            }
        }

        public virtual void OnViewVisible(bool visibleState)
        {
            if (CameraInstance != null)
            {
                CameraInstance.SetVisibleState(visibleState);
            }
        }
    }
}