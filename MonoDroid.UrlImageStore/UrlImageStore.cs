using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.Graphics.Drawables;

namespace MonoDroid.UrlImageStore
{
	public interface IUrlImageUpdated<TKey>
	{
		void UrlImageUpdated(TKey id);
	}

	public class UrlImageStore<TKey>
	{
		public delegate Drawable ProcessImageDelegate(Drawable img, TKey id);
		//static NSOperationQueue opQueue;
		
		readonly static string baseDir;
		
		string picDir;
				
		LRUCache<TKey, Drawable> cache;
		//Queue<UrlImageStoreRequest<TKey>> queue;

		//LimitedConcurrencyLevelTaskScheduler taskScheduler;
		TaskFactory taskFactory;

		object taskLock = new object();
		long taskCount = 0;
		ConcurrentQueue<Action> queuedActions;

		static UrlImageStore()
		{

			baseDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal); ;
		}
		
		
		public UrlImageStore(int capacity, string storeName, Drawable defaultImage, ProcessImageDelegate processImage, int maxConcurrency = 2)
		{
			//this.queue = new Queue<UrlImageStoreRequest<TKey>>();
			this.Capacity = capacity;
			this.StoreName = storeName;
			this.ProcessImage = processImage;
			this.DefaultImage = defaultImage;
			this.MaxConcurrency = maxConcurrency;
			//this.taskScheduler = new LimitedConcurrencyLevelTaskScheduler(maxConcurrency);
			//this.taskFactory = new TaskFactory(this.taskScheduler);

			cache = new LRUCache<TKey, Drawable>(capacity);
			
			if (!Directory.Exists(Path.Combine(baseDir, "Caches/Pictures/" + storeName)))
				Directory.CreateDirectory(Path.Combine(baseDir, "Caches/Pictures/" + storeName));
			
			picDir = Path.Combine(baseDir, "Caches/Pictures/" + storeName).TrimEnd('/') + "/";



			queuedActions = new ConcurrentQueue<Action>();
		}
		
		public void DeleteCachedFiles()
		{
			string[] files = new string[]{};
			
			try 
			{ 
				files = Directory.GetFiles(picDir); 
			}
			catch (Exception ex) 
			{
				Console.WriteLine("GETFILES FAILED: " + ex.ToString());
			}

			if (files == null || files.Length == 0)
				Console.WriteLine("Found NO Files to delete");
			else
				Console.WriteLine("Found Files to Delete: " + files.Length);
	
			foreach (string file in files)
			{
				try 
				{
					Console.WriteLine("Deleting: " + file);
					File.Delete(file); 
				}
				catch (Exception ex)
				{
					Console.WriteLine("Failed to Delete: " + file + " -> " + ex.ToString());
				}
			}
		}

		public int MaxConcurrency
		{
			get;
			set;
		}

		public ProcessImageDelegate ProcessImage
		{
			get;
			private set;
		}
		
		public int Capacity
		{
			get;
			private set;
		}

		public Drawable DefaultImage
		{
			get;
			set;
		}
		
		public string StoreName
		{
			get;
			private set;	
		}

		public Drawable GetImage(TKey id)
		{
			Drawable result = this.DefaultImage;

			lock (cache)
			{
				try
				{
					if (cache.ContainsKey(id))
						result = cache[id];
				}
				catch { }
			}

			return result;
		}
		
		public bool Exists(TKey id)
		{
			lock (cache)
			{
				return cache.ContainsKey(id);	
			}
		}

		public Drawable RequestImage(TKey id, string url, Action<TKey> notify) // IUrlImageUpdated<TKey> notify)
		{
			//First see if the image is in memory cache already and return it if so
			lock (cache)
			{
				try
				{
					if (cache.ContainsKey(id))
						return cache[id];
				}
				catch { }
			}
			
			//Next check for a saved file, and load it into cache and return it if found
			string picFile = picDir + id + ".png";

			if (File.Exists(picFile))
			{
				Drawable img = null;
				
				try { img = Drawable.CreateFromPath(picFile); }
				catch { }
				
				if (img != null)
				{					
					AddToCache(id, img); //Add it to cache
					return img; //Return this image
				}
			}

			//Add the request to the task queue
			queuedActions.Enqueue(() =>
			{
				Drawable drawable = null;
				var itemId = id;
				var itemUrl = url;
				var itemNotify = notify;

				try
				{
					var req = System.Net.HttpWebRequest.Create(itemUrl.Trim('\"')) as System.Net.HttpWebRequest;
					using (var stream = req.GetResponse().GetResponseStream())
					{
						drawable = Drawable.CreateFromStream(stream, "");
					}
				}
				catch { }

				if (drawable != null)
				{
					if (this.ProcessImage != null)
						drawable = this.ProcessImage(drawable, itemId);

					AddToCache(itemId, drawable);

					if (notify != null)
						notify(itemId);
					//if (notify != null)
					//	notify.UrlImageUpdated(itemId);
				}
			});

			lock (taskLock)
			{
				if (System.Threading.Interlocked.Read(ref taskCount) < MaxConcurrency)
				{
					System.Threading.ThreadPool.QueueUserWorkItem((state) => {

						System.Threading.Interlocked.Increment(ref taskCount);
						//taskCount++;

						try
						{
						while (queuedActions.Count > 0)
						{
							Action toDo = null;

							if (queuedActions.TryDequeue(out toDo))
							{
								if (toDo != null)
								{
									try { toDo(); }
									catch (Exception ex) 
									{
										Console.WriteLine("Task Had Exception: " + ex.ToString());
									}
								}
							}
						}
						}
						catch {}

						//taskCount--;
						System.Threading.Interlocked.Decrement(ref taskCount);
					});

				}
			}

			return this.DefaultImage;
		}

		internal void AddToCache(TKey id, Drawable img)
		{
			lock (cache)
			{
				if (cache.ContainsKey(id))
					cache[id] = img;
				else
					cache.Add(id, img);
			}
			
			string picFile = picDir + id + ".png";

			if (!File.Exists(picFile))
			{
				var bmp = ((BitmapDrawable)img).Bitmap;

				using (var fos = System.IO.File.OpenWrite(picFile))
				{
					bmp.Compress(Android.Graphics.Bitmap.CompressFormat.Png, 100, fos);
				}
			}
		}

		public void ReclaimMemory()
		{
			lock (cache)
			{
				cache.ReclaimLRU(cache.Count / 4);
			}
		}
	}

	public class UrlImageStoreRequest<TKey>
	{
		public TKey Id
		{
			get;
			set;
		}

		public string Url
		{
			get;
			set;
		}

		public IUrlImageUpdated<TKey> Notify
		{
			get;
			set;
		}
	}
}
