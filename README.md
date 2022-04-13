# conical-autoUploader
This is an example project to automatically populate a conical instance with fake data. The intention is that it covers all of the different types of data which could be published to the tool, both to demonstrate the tool's usage as well as to verify that the expected functionality is correctly functioning.

This project is used internally to ensure that https://demo.conical.cloud has daily fresh data as well as being used to populate an internal 'final checks' instance prior to new versions of the tool being released. 

It is made public to give additional examples of possible usage patterns for the tool as well as to provide 'hints and tips' for doing so.

## Instructions
If you're planning to use the tool to populate an instance, then the following steps are necessary:

* Create an access token (server:accessToken) granting write access to the products

To run in one off mode:

```dotnet AutomaticUploader.dll --server:url=http://localhost:44316 --server:accessToken="dsflkjkdfjlkdf344356gsf" --lifetime:mode=oneoff --telegram:chatID="" --telegram:botKey=""```

To run in continuous mode:

```dotnet AutomaticUploader.dll --server:url=http://localhost:44316 --server:accessToken="dsflkjkdfjlkdf344356gsf" --lifetime:mode=continuous --telegram:chatID="" --telegram:botKey=""```


Note that that's not our actual access token :-) 

## Operating modes

### Looping
In this mode, the tool will constantly be running in the background and will check on a regular basis (by default, every 60s) whether new data needs to be published and do so accordingly. This mode is used to ensure that the demo instance of conical is constantly up to data with the latest sample data.

This is the default mode, but can be explicitly configured with the following setting:

|Property|Description|
|--|--|
|lifetime:mode|continuous|

### One-off
In this mode, the tool will check and populate an instance as necessary and then exit. This mode is used internally to populate a 'final checks' instance prior to releases to ensure that every thing is correct.

To enable this mode, the following settings can be used:

|Property|Description|
|--|--|
|lifetime:mode|oneoff|

### Telegam Notifications
The tool can optionally report, via a Telgram bot, when it is publishing data. To do so, the following properties need to be:

|Property|Description|
|--|--|
|telegram:chatid|The Chat ID to use|
|telegram:botKey|The bot key to use|