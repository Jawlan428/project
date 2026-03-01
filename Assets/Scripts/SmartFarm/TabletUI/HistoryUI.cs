using System.Collections.Generic;
using UnityEngine;

namespace SmartFarm
{
    public class HistoryUI : MonoBehaviour
    {
        [SerializeField] private FarmDataManager dataManager;
        [SerializeField] private Transform listRoot;
        [SerializeField] private HistoryListItemUI itemPrefab;
        [SerializeField] private GameObject emptyStateRoot;
        [SerializeField] private int maxVisibleItems = 40;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void OnEnable()
        {
            if (dataManager == null) dataManager = FindFirstObjectByType<FarmDataManager>();
            if (dataManager != null)
            {
                dataManager.OnHistoryChanged += OnHistoryChanged;
                OnHistoryChanged(dataManager.History);
            }
        }

        private void OnDisable()
        {
            if (dataManager != null)
                dataManager.OnHistoryChanged -= OnHistoryChanged;
        }

        private void OnHistoryChanged(IReadOnlyList<FarmHistoryItem> history)
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();

            int count = history != null ? Mathf.Min(maxVisibleItems, history.Count) : 0;
            if (emptyStateRoot != null) emptyStateRoot.SetActive(count == 0);
            if (count == 0 || itemPrefab == null || listRoot == null) return;

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(itemPrefab.gameObject, listRoot);
                var ui = go.GetComponent<HistoryListItemUI>();
                if (ui != null) ui.Bind(history[i]);
                _spawned.Add(go);
            }
        }
    }
}
