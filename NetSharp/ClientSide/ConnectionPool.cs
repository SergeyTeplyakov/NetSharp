using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Communications;

namespace NetSharp.ClientSide
{
    public abstract class Pool<K, T, F>
    {
        int instanceCount;

        public int InstanceCount => instanceCount;

        protected readonly ConcurrentDictionary<K, T> map;

        protected Pool()
        {
            map = new ConcurrentDictionary<K, T>();
        }

        protected abstract bool Compare(F key, T value);

        protected bool TryRemove(F key, out T item)
        {
            item = default(T);

            foreach (var pair in map)
                if (Compare(key, pair.Value) && map.TryRemove(pair.Key, out item))
                    return true;

            return false;
        }

        protected abstract Task<T> Create(F key);

        public async Task<T> Take(F key)
        {
            T item;

            if (TryRemove(key, out item))
            {
                Interlocked.Decrement(ref instanceCount);
                return item;
            }
            else
            {
                item = await Create(key).ConfigureAwait(false);

                Interlocked.Increment(ref instanceCount);

                return item;
            }
        }

        public virtual void Release(K key, T item)
        {
            if (!map.TryAdd(key, item))
                throw new ArgumentException($"Не удалось вернуть в пул объект с ключём {key}.");
        }
    }

    /// <summary>
    /// Пул неиспользуемых прикладным кодом, но активных, подключений.
    /// </summary>
    public class ConnectionPool : Pool<Guid, Connection, int>
    {
        RemoteClient client;

        public ConnectionPool(RemoteClient client)
        {
            this.client = client;
        }

        protected override async Task<Connection> Create(int key)
        {
            return await client.ConnectToAnotherHandlerAsync(new IPEndPoint(client.RemoteIPAddress, key)).ConfigureAwait(false);
        }

        public IEnumerator<Connection> GetEnumerator()
        {
            foreach(var pair in map)
                yield return pair.Value;
        }

        protected override bool Compare(int key, Connection value)
        {
            return value.RemoteEndPoint.Port == key;
        }
    }
}
