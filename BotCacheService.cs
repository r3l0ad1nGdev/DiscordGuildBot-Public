using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Linq;

namespace GuildBot
{
    interface IKeepUntil
    {
        DateTime KeepUntil { get;}
    }
    class KeepUntilCacheItem : IKeepUntil
    {
        DateTime _keepUntil;
        public object Data { get; set; }
        public DateTime KeepUntil => _keepUntil;
        public KeepUntilCacheItem(DateTime keepUntil)
        {
            _keepUntil = keepUntil;
        }
    }
    class CacheItem
    {
        internal DateTime LastAccessed { get; set; }
        internal object Data { get; set; }

    }
    public class BotCacheService
    {
        static readonly BotCacheService _instance = new BotCacheService();
        Dictionary<string, CacheItem> _store;


        public static BotCacheService Instance { get => _instance; }

        private BotCacheService() 
        {
            _store = new Dictionary<string, CacheItem>();
            var cleaner = new BackgroundWorker();
            cleaner.WorkerSupportsCancellation = true;
            cleaner.DoWork += Cleaner_DoWork;
            cleaner.RunWorkerAsync();
        }

        private void Cleaner_DoWork(object sender, DoWorkEventArgs e)
        {
            var cleaner = (BackgroundWorker)sender;
            //duration of time before an item is marked for purge
            var tooOld = new TimeSpan(0, 5, 0);
            while (!cleaner.CancellationPending)
            {
                Thread.Sleep((int)tooOld.TotalMilliseconds/2);
                lock(_store) 
                {
                    var now = DateTime.Now;
                    var thingsToRemove = _store.Keys.Where(id => (now - _store[id].LastAccessed) >= tooOld);
                    foreach (var id in thingsToRemove)
                    {
                        var item = _store[id];
                        
                        if (item.Data is KeepUntilCacheItem kuci && kuci.KeepUntil > now )
                        {
                            continue;
                        }
                        _store.Remove(id);
#if DEBUG 
                        Console.WriteLine($"{id} has been removed from the cache");
#endif
                    }
                        
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        internal bool IsEmpty
        {
            get
            {
                lock (_store)
                {
                    return _store.Count == 0;
                }
            }
        }

        public object Get(string id)
        {
            lock (_store)
            {
                var result = _store[id];
                RefreshAccess(id, result);
                return result.Data;
            }
        }

        void RefreshAccess(string id, CacheItem old)
        {
            _store[id] = new CacheItem { Data = old.Data, LastAccessed = DateTime.Now };

        }
        /// <summary>
        /// Destructive reading helps clean up memory that is no longer required.
        /// TODO how will we clean the memory used by people that never submit the app or cancel
        /// for example: we can have a background process that removes stale i.e. 10 min old data in memory.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="destructiveRead"></param>
        /// <returns></returns>
        public object Get(string id, bool destructiveRead)
        {
            lock (_store)
            {
                var result = _store[id];
                if (destructiveRead)
                {
                    _store.Remove(id);
                }
                else
                {
                    RefreshAccess(id, result);
                }
                return result.Data;
            }
        }

        public bool ContainsKey(string id)
        {
            lock (_store)
            {
                return _store.ContainsKey(id);
            }
        }
        public T TryGet<T>(string id, T defaultValue, bool destructiveRead)
        {
            lock (_store)
            {
                if (_store.TryGetValue(id, out var result))
                {
                    if (destructiveRead)
                    {
                        _store.Remove(id);
                    }
                    else
                    {
                        RefreshAccess(id, result);
                    }
                    return (T)result.Data;
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// TODO wrap state into another object that has a timestamp
        /// </summary>
        /// <param name="id"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public BotCacheService Put(string id, object state)
        {
            lock (_store)
            {
                _store[id] = new CacheItem { Data = state, LastAccessed = DateTime.Now } ;
                return this;
            }
        }
    }
}
