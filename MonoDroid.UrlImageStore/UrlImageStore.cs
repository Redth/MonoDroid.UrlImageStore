using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
						
		static UrlImageStore()
		{
			baseDir  = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "..");
			
			//opQueue = new NSOperationQueue();
			//opQueue.MaxConcurrentOperationCount = 4;
		}
		
		public static int MaxConcurrentOperationCount
		{
			get; //{ return opQueue.MaxConcurrentOperationCount; }
			set; //{ opQueue.MaxConcurrentOperationCount = value; }
		}
		
		public UrlImageStore(int capacity, string storeName, ProcessImageDelegate processImage)
		{
			this.Capacity = capacity;
			this.StoreName = storeName;
			this.ProcessImage = processImage;
			
			cache = new LRUCache<TKey, Drawable>(capacity);
			
			if (!Directory.Exists(Path.Combine(baseDir, "Library/Caches/Pictures/")))
				Directory.CreateDirectory(Path.Combine(baseDir, "Library/Caches/Pictures/"));
			
			picDir = Path.Combine(baseDir, "Library/Caches/Pictures/" + storeName);
			
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
				if (cache.ContainsKey(id))
					result = cache[id];
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
				if (cache.ContainsKey(id))
					return cache[id];
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

			//At this point the file needs to be downloaded, so queue it up to download
			//lock (queue)
		//	{
		//		queue.Enqueue(new UrlImageStoreRequest<TKey>() { Id = id, Url = url, Notify = notify });
		//	}

			//If we haven't maxed out our threads we should start another to download the images
		//	if (threadCount < maxWorkers)
		//		ThreadPool.QueueUserWorkItem(new WaitCallback(DownloadImagesWorker));
		
			//opQueue.AddOperation(new DownloadImageOperation<TKey>(this, id, url, notify));
			
			opQueue.AddOperation(delegate {

				string file = picDir + id + ".png";

				var fos = new Java.IO.FileOutputStream(file);
				fos.Write((new Java.Net.URL(url).OpenStream()).
			
				img = this.ProcessImage(img, id);
			
				this.AddToCache(id, img);
			
				notify.UrlImageUpdated(id);	
			});
			
			//Return the default while they wait for the queued download
			return this.DefaultImage;
		}

//		void DownloadImagesWorker(object state)
//		{
//			threadCount++;
//
//			UrlImageStoreRequest<TKey> nextReq = null;
//			
//			while ((nextReq = GetNextRequest()) != null)
//			{
//				UIImage img = null;
//
//				
//				try { img = UIImage.LoadFromData(NSData.FromUrl(NSUrl.FromString(nextReq.Url))); }
//				catch (Exception ex) 
//				{
//					Console.WriteLine("Failed to Download Image: " + ex.Message + Environment.NewLine + ex.StackTrace);
//				}
//				
//				if (img == null)
//					continue;
//
//				//See if the consumer needs to do any processing to the image
//				if (this.ProcessImage != null)
//					img = this.ProcessImage(img, nextReq.Id);
//					
//				//Add it to cache
//				AddToCache(nextReq.Id, img);
//
//			
//				
//				//Notify the listener waiting for this,
//				// but do this on the main thread so the user of this class doesn't worry about that
//				//nsDispatcher.BeginInvokeOnMainThread(delegate { nextReq.Notify.UrlImageUpdated(nextReq.Id); });
//				nextReq.Notify.UrlImageUpdated(nextReq.Id);
//			}
//
//			threadCount--;
//		}

		internal void AddToCache(TKey id, Drawable img)
		{
			lock (cache)
			{
				if (cache.ContainsKey(id))
					cache[id] = img;
				else
					cache.Add(id, img);
			}
			
		
			//string file = picDir + id + ".png";
			
			//if (!File.Exists(file))
			//{
			//    img.w
			//    //Save it to disk
			//    NSError err = null;
			//    try 
			//    { 
			//        img.AsPNG().Save(file, false, out err); 
			//        if (err != null)
			//            Console.WriteLine(err.Code.ToString() + " - " + err.LocalizedDescription);
			//    }
			//    catch (Exception ex) 
			//    {
			//        Console.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
			//    }
			//}
		}
		
//		UrlImageStoreRequest<TKey> GetNextRequest()
//		{
//			UrlImageStoreRequest<TKey> nextReq = null;
//
//			lock (queue)
//			{
//				if (queue.Count > 0)
//					nextReq = queue.Dequeue();
//			}
//
//			return nextReq;
//		}

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
