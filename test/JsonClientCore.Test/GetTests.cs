using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JsonClientCore.Models;
using JsonClientCore.Test.Helpers;
using Xunit;

namespace JsonClientCore.Test
{
	public class GetTests
	{
		[Fact]
		public async Task Test_Basic_Get_Request_Async()
		{
			var client = new JsonClient(new Uri("https://baelor.io/api/v0/"), new Options
			{
				Headers = new Dictionary<string, string>
				{
					{ "Authorization", $"bearer {EnvironmentVariableHelpers.GetBaelorApiKeyVariable()}" }
				}
			});

			dynamic response = await client.RequestAsync<object, object, object>("GET", "/songs/style");

			Assert.Equal("style", response.result.slug);
		}
	}
}
