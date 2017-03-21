using System;

namespace NetSharp
{
    // Пока не понял, что это и зачем.
    // Знаешь ли про Lazy<T>?
    class RecordOnce<T>
    {
        bool recorded;
        T value;

        /// <summary>
        /// Инициализация значением по-умолчанию.
        /// </summary>
        public RecordOnce()
        {
            value = default(T);
        }

        /// <summary>
        /// Инициализация значением передаваемым пользователем.
        /// </summary>
        ///<param name="initValue">Значение.</param>
        ///<param name="changeValue">Указывает на возможность однократного изменения переданного значения.</param>
        public RecordOnce(T initValue, bool changeValue)
        {
            value = initValue;
            recorded = !changeValue;
        }

        public T Value
        {
            get
            {
                return value;
            }
            set
            {
                if (recorded)
                    throw new ArgumentException("Значение уже было изменено. Повторное изменение не допускается.");

                recorded = true;
                this.value = value;
            }
        }

        public static implicit operator T(RecordOnce<T> recordOnce)
        {
            return recordOnce.value;
        }
    }
}
