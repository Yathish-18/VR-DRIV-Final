using UnityEngine;

namespace Gley.UrbanSystem.Internal
{
    [System.Serializable]
    public class CellProperties
    {
        [SerializeField] private Vector3 _center;
        [SerializeField] private int _row;
        [SerializeField] private int _column;

        public int Row => _row;
        public int Column => _column;

        public Vector3 Center
        {
            get
            {
                return _center;
            }
            set
            {
                _center = value;
            }
        }

        public CellProperties(int column, int row, Vector3 center)
        {
            _center = center;
            _row = row;
            _column = column;
        }
    }
}