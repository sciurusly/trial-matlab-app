# trial-matlab-app
Trial app that listens for updates from firebase and then updates the relevant model.

## Usage
The app is run with the command:
```
MatlabApp db-name secret root
```
The arguments are:
db-name is the public name of the firebase database; e.g. `stage-financial-canvas-studio`
secret is the secret provided by firebase for the database
root is the root key in the database.

This starts the app running and listens on the provided keys `_callback` subkey. The expcted structure is:
```
{
  "model" : "Model Name",
  "post" : "SaveExternal",
  "state" : "State (n)",
  "update" : {
    "JourneyPlan" : {
      "InitialAssets" : {
        "type" : "double",
        "value" : 110000000
      }
    }
  }
}
The keys inside `update` and the value of `post` are the names of blocks within the model.
With `update.Block Name` multiple fields can be provided. If any fields update then the model is updated with all the values for the block and then the named post block is refreshed for external source. 


# v0.0
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
