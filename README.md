# trial-matlab-app
Trial using the Canvas API within C#

This self contained test calls the .net assembly API.dll to access canvas

Usage:

```
var api = new API();
```
Create an instance of the `api` to be called.
This is long running as it needs to load the runtime


```
api.LoadState(name, state);
```
Loads up the model state from `CanvasFiles\<name>\<state>.state`


```
api.SetProperty(numRets, id, field, value, [force]);
```
Update a property on a block. `id` is the block's id, `field` the name of a tunable property and `value` the new value.

If `numRets` is `0` there is no return, `1` gives a flag if the value changed - `0` or `1`.

Sending `force` as `1` ensures the value is refreshed even if unchanged.


```
var value = api.GetProperty(1, id, field);
```
Returns the current value of `field` for the block.

## Models
The app requires an existing model on the test machine in the shared folder.
This will need to be copied over as a seperate step after installing from the inernal file server. `C:\Shares\FinancialCanvas\sciurus\Account\CanvasFiles\API Test`
