using System;
using System.Collections.Generic;
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

		LimitedConcurrencyLevelTaskScheduler taskScheduler;
		TaskFactory taskFactory;

		static UrlImageStore()
		{
			baseDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal); ;
		}
		
		
		public UrlImageStore(int capacity, string storeName, Drawable defaultImage, ProcessImageDelegate processImage, int maxConcurrency = 4)
		{
			//this.queue = new Queue<UrlImageStoreRequest<TKey>>();
			this.Capacity = capacity;
			this.StoreName = storeName;
			this.ProcessImage = processImage;
			this.DefaultImage = defaultImage;
			this.taskScheduler = new LimitedConcurrencyLevelTaskScheduler(maxConcurrency);
			this.taskFactory = new TaskFactory(this.taskScheduler);

			cache = new LRUCache<TKey, Drawable>(capacity);
			
			if (!Directory.Exists(Path.Combine(baseDir, "Caches/Pictures/")))
				Directory.CreateDirectory(Path.Combine(baseDir, "Caches/Pictures/"));
			
			picDir = Path.Combine(baseDir, "Caches/Pictures/" + storeName);
			
		}
		
		public void DeleteCachedFiles()
		{
			string[] files = new string[]{};
			
			try { files = Directory.GetFiles(picDir); }
			catch { }
			
			foreach (string file in files)
			{
				try { File.Delete(file); }
				catch { }
			}
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

		public Drawable RequestImage(TKey id, string url, IUrlImageUpdated<TKey> notify)
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
			
			taskFactory.StartNew(() =>
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
						notify.UrlImageUpdated(itemId);
				}
			});
			
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
