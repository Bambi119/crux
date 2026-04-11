using UnityEngine;

namespace Crux.Core
{
    /// <summary>오브젝트의 진영을 식별</summary>
    public class SideIdentifier : MonoBehaviour
    {
        [SerializeField] private PlayerSide side;
        public PlayerSide Side => side;

        public void SetSide(PlayerSide newSide)
        {
            side = newSide;
        }
    }
}
