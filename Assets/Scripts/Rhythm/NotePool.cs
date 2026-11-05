using System.Collections.Generic;
using UnityEngine;

namespace Rhythm
{
    /// <summary>
    /// 音符对象池：预热若干个，耗尽时按需扩容（池数量不写死）。
    /// </summary>
    public class NotePool
    {
        private readonly Queue<NoteController> free = new Queue<NoteController>();
        private readonly Transform parent;
        private readonly bool canExpand;
        private readonly System.Func<NoteController> createInstance;

        public int FreeCount { get { return free.Count; } }
        public int TotalCreated { get; private set; }

        public NotePool(Transform parent, bool canExpand, System.Func<NoteController> createInstance, int preloadCount)
        {
            this.parent = parent;
            this.canExpand = canExpand;
            this.createInstance = createInstance;

            for (int i = 0; i < preloadCount; i++)
            {
                free.Enqueue(Create());
            }
        }

        public NoteController Get()
        {
            if (free.Count == 0)
            {
                if (!canExpand)
                {
                    Debug.LogError("[NotePool] 池已耗尽且禁止扩容");
                    return null;
                }

                free.Enqueue(Create());
            }

            return free.Dequeue();
        }

        public void Release(NoteController note)
        {
            if (note == null)
            {
                return;
            }

            note.Unbind();
            note.gameObject.SetActive(false);
            free.Enqueue(note);
        }

        private NoteController Create()
        {
            NoteController instance = createInstance();

            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            instance.gameObject.SetActive(false);
            TotalCreated++;
            return instance;
        }
    }
}
