using UnityEngine;

namespace DrJones
{
    [CreateAssetMenu(fileName = "PrefabLibrary", menuName = "Dr Jones/Prefab Library")]
    public sealed class PrefabLibrary : ScriptableObject
    {
        public GameObject FloorTile;
        public GameObject WallBlock;
        public GameObject BreakableWall;
        public GameObject Player;
        public GameObject Boulder;
        public GameObject SpecialBoulder;
        public GameObject FertilityIdol;
        public GameObject ThorFragment;
        public GameObject ThorHammer;
        public GameObject ExitMarker;
        public GameObject Heart;
    }
}
