﻿using System;

namespace JsonClientCore.Test.Helpers
{
	public static class EnvironmentVariableHelpers
	{
		public static string GetBaelorApiKeyVariable()
		{
			return Environment.GetEnvironmentVariable("JSONCLIENT_BAELOR_TEST_KEY");
		}
	}
}