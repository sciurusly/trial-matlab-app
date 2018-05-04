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
```

The keys inside `update` and the value of `post` are the names of blocks within the model.
With `update.Block Name` multiple fields can be provided. If any fields update then the model is updated with all the values for the block and then the named post block is refreshed for external source. 

## Life Cycle
The proposed lifecycle would be:

1. From a saved state, Canvas writes the details to Firebase.
The components do not need to be aware of the usage, but they do need to describe themselves.
So JourneyPlan will include the tunable properties for anything that we may wish to update from studio.
2. Studio writes the details of what it will allow to be entered to the `_callback` block. This is the are that will be watched and any updates picked up.
3. User goes into update mode.  This requires that the listener process is active; ** currently out of scope for POC **
4. Updates are written to the `_callback.update` area.
5. The listener picks up any updates and uses them to refresh the loaded model.
6. The listener updates the model state named in firebase with new data fromthe update block
7. Ufter updating the model, the named `post` block is refreshed, this will cause the new model data to be written to firebase (step 1).

![Update Cycle](https://github.com/sciurusly/trial-matlab-app/blob/master/update%20cycle%20for%20firebase.png "Update Cycle")

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
