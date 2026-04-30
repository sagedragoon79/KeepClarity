using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FFUIOverhaul.TechTree
{
    /// <summary>
    /// Polled input handler for queuing tech nodes. Right-click is reserved by
    /// the vanilla OnPointerClick handler (which spends a KP), so we use a
    /// hotkey instead: with the tech tree open, hover any node and press Q to
    /// toggle it in the queue.
    ///
    /// Raycast through the EventSystem to find which node is under the mouse —
    /// avoids any reliance on Harmony patching of explicit interface methods,
    /// which crashed the tech tree previously.
    /// </summary>
    public static class TechQueueInput
    {
        public static KeyCode QueueToggleKey = KeyCode.Q;
        private static readonly List<RaycastResult> _raycastBuf = new();

        public static void Tick()
        {
            if (!Input.GetKeyDown(QueueToggleKey)) return;
            var es = EventSystem.current;
            if (es == null) return;

            var data = new PointerEventData(es) { position = Input.mousePosition };
            _raycastBuf.Clear();
            es.RaycastAll(data, _raycastBuf);
            foreach (var hit in _raycastBuf)
            {
                if (hit.gameObject == null) continue;
                var node = hit.gameObject.GetComponentInParent<TechTreeNode>();
                if (node == null) continue;
                int id = node.GetId();
                if (id >= 0) TechAutoQueue.Toggle(id);
                return;
            }
        }
    }
}
