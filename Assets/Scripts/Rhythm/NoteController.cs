using UnityEngine;
using UnityEngine.UI;

namespace Rhythm
{
    /// <summary>
    /// 单个音符的池对象。位置、判定都由 RhythmGameManager 驱动，本类只负责数据和外观。
    /// </summary>
    public class NoteController : MonoBehaviour
    {
        public Image Image { get; private set; }
        public NoteData Data { get; private set; }

        private void Awake()
        {
            Image = GetComponent<Image>();
        }

        /// <summary>从池取出时绑定数据</summary>
        public void Bind(NoteData data)
        {
            Data = data;
            data.controller = this;
            data.state = NoteState.OnScreen;
        }

        /// <summary>回池前的清理</summary>
        public void Unbind()
        {
            if (Data != null)
            {
                Data.controller = null;
                Data.state = NoteState.Judged;
            }

            Data = null;
        }
    }
}
