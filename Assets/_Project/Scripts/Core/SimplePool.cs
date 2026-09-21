using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 极简 GameObject 对象池。
    /// 策划案要求「怪物每层共享一个对象池」，法术走同一套池。
    /// </summary>
    public class SimplePool
    {
        private readonly GameObject _prefab;
        private readonly Transform _root;
        private readonly Stack<GameObject> _idle = new Stack<GameObject>();

        public SimplePool(GameObject prefab, Transform root, int prewarm = 0)
        {
            _prefab = prefab;
            _root = root;
            for (int i = 0; i < prewarm; i++)
            {
                var go = Object.Instantiate(_prefab, _root);
                go.SetActive(false);
                _idle.Push(go);
            }
        }

        public GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            GameObject go = _idle.Count > 0 ? _idle.Pop() : Object.Instantiate(_prefab, _root);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(_root, false);
            _idle.Push(go);
        }

        public int IdleCount => _idle.Count;
    }

    /// <summary>池化对象统一用这个组件回收，避免各处自己记池引用。</summary>
    public class PooledObject : MonoBehaviour
    {
        public SimplePool Owner;
        public void Release()
        {
            if (Owner != null) Owner.Despawn(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
