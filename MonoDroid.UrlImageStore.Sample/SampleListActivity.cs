using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace MonoDroid.UrlImageStore.Sample
{
	[Activity(Label = "MonoDroid.UrlImageStore.Sample", MainLauncher = true, Icon = "@drawable/icon")]
	public class SampleListActivity : ListActivity
	{
		SampleListAdapter adapter;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			adapter = new SampleListAdapter(this);

			this.ListAdapter = adapter;

			adapter.Search("#monodroid");		
		}
	}


	public class SampleListAdapter : BaseAdapter<Tweet>, IUrlImageUpdated<string>
	{
		MonoDroid.UrlImageStore.UrlImageStore<string> urlImageStore;

		public Activity Owner { get; set; }
		public List<Tweet> Tweets { get; set; }

		public void Search(string terms)
		{
			Task.Factory.StartNew(() =>
			{
				this.Tweets = Twitter.SearchTweets(0, terms);

				this.Owner.RunOnUiThread(() =>
				{
					this.NotifyDataSetChanged();
				});
			});
		}

		public SampleListAdapter(Activity owner)
			: base()
		{
			this.Owner = owner;
			this.Tweets = new List<Tweet>();
			var defImage = Owner.Resources.GetDrawable(Resource.Drawable.EmptyPic);

			this.urlImageStore = new UrlImageStore<string>(100, "TwitterPics", defImage, new UrlImageStore<string>.ProcessImageDelegate((Android.Graphics.Drawables.Drawable img, string id) =>
			{
				//Don't do any preprocessing
				return img;

			}), 4);
		}

		public override Tweet this[int position]
		{
			get { return Tweets[position]; }
		}

		public override int Count
		{
			get { return Tweets.Count; }
		}

		public override long GetItemId(int position)
		{
			return position;
		}

		public override View GetView(int position, View convertView, ViewGroup parent)
		{
			//Get our object for this position
			var tweet = Tweets[position];

			//Try to reuse convertView if it's not  null, otherwise inflate it from our item layout
			// This gives us some performance gains by not always inflating a new view
			// This will sound familiar to MonoTouch developers with UITableViewCell.DequeueReusableCell()

			LinearLayout view = null;

			if (convertView != null && convertView is LinearLayout)
				view = (LinearLayout)convertView;
			else
				view = (LinearLayout)LayoutInflater.FromContext(this.Owner).Inflate(Resource.Layout.TweetListItem, null);

			//Find references to each subview in the list item's view
			var pic = view.FindViewById<ImageView>(Resource.Id.Pic);

			//imageItem.LayoutParameters = new ViewGroup.LayoutParams(44, 44);
			//pic.SetScaleType(ImageView.ScaleType.FitStart);

			var userHandle = view.FindViewById<TextView>(Resource.Id.UserHandle);
			var tweetText = view.FindViewById<TextView>(Resource.Id.Tweet);


			pic.SetImageDrawable(urlImageStore.RequestImage(tweet.FromUser.Trim('@'), tweet.ProfileImgUrl, this));
			userHandle.Text = "@" + tweet.FromUser.Trim('@');
			tweetText.Text = tweet.Text;
	
			return view;
		}

		public void UrlImageUpdated(string id)
		{
			this.Owner.RunOnUiThread(() =>
			{
				this.NotifyDataSetChanged();
			});
		}
	}
}

