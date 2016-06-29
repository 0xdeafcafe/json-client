using Newtonsoft.Json;

namespace JsonClientCore.Models
{
	public class Error<T>
	{ }

	public class Error
		: Error<object>
	{ }
}
