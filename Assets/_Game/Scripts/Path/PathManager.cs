using System.Collections.Generic;
using UnityEngine;

namespace FinalDefense.Path
{
    public class PathManager : MonoBehaviour
    {
        public static PathManager Instance { get; private set; }

        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        public int WaypointCount => waypoints.Count;

        private void Awake()
        {
            Instance = this;
        }

        public Transform GetWaypoint(int index)
        {
            if (index < 0 || index >= waypoints.Count) return null;
            return waypoints[index];
        }

        public Vector3 GetWaypointPosition(int index)
        {
            var wp = GetWaypoint(index);
            return wp != null ? wp.position : Vector3.zero;
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Count < 2) return;

            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.2f);
                if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }
        }
    }
}
