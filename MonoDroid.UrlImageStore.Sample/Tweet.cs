using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq;
using System.Net;
using System.Json;
using System.Web;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace MonoDroid.UrlImageStore.Sample
{
	public class Tweet
	{
		public long Id { get; set; }
		public string ProfileImgUrl { get; set; }
		public string Text { get; set; }
		public string FromUser { get; set; }
	}


	public class Twitter
	{
		public static List<Tweet> SearchTweets(long sinceId, params string[] terms)
		{
			var tweets = new List<Tweet>();

			var query = string.Format("{0}?q={1}&since_id={2}&lang=en",
				"http://search.twitter.com/search.json",
				HttpUtility.UrlEncode(string.Join(" OR ", terms)),
				sinceId);
			
			var data = (new WebClient()).DownloadString(query);

			var jsonObj = JsonObject.Parse(data) as JsonObject;
			var jsonResults = jsonObj["results"] as JsonArray;

			foreach (var jsonItem in jsonResults)
			{
				tweets.Add(new Tweet()
				{
					Id = (long)jsonItem["id"],
					ProfileImgUrl = jsonItem["profile_image_url"].ToString().Trim('\"'),
					Text = jsonItem["text"].ToString().Trim('\"'),
					FromUser = jsonItem["from_user"].ToString().Trim('\"')
				});
			}

			return tweets;
		}
	}
}
