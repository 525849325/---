using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmortalLoot.Core
{
    public sealed class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly Stack<GameObject> _available = new Stack<GameObject>();
        private readonly HashSet<GameObject> _leased = new HashSet<GameObject>();

        public int CreatedCount { get; private set; }
        public int ActiveCount => _leased.Count;

        public GameObjectPool(GameObject prefab, Transform root, int preload = 0)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _root = root;
            for (var i = 0; i < preload; i++) _available.Push(Create());
        }

        public GameObject Rent()
        {
            var value = _available.Count > 0 ? _available.Pop() : Create();
            value.SetActive(true);
            _leased.Add(value);
            return value;
        }

        public void Return(GameObject value)
        {
            if (value == null || !_leased.Remove(value)) return;
            value.SetActive(false);
            value.transform.SetParent(_root, false);
            _available.Push(value);
        }

        private GameObject Create()
        {
            var value = UnityEngine.Object.Instantiate(_prefab, _root);
            value.SetActive(false);
            CreatedCount++;
            return value;
        }
    }
}
