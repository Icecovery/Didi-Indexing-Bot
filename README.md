# Didi Indexing Bot

An indexing and archiving bot for telegram

## To Use This Bot

0. Create your bot using bot father, remember to change `Group Privacy` in the bot father setting to `disable` so that the bot can archive messages

1. Add user secret in visual studio (follow [this guide](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-6.0&tabs=windows#json-structure-flattening-in-visual-studio)). Or do it using the command line, follow the previous section in the same guide.

	* `botAPIKey`

		* Your bot API from bot father
	
	* `groupId`

		* Your target group id

2. change BotName in Program.cs to your own bot username with a '@' at front (i.e.: if you your bot username is `mybot`, change it to `@mybot`)

3. While you are there, copy the `Bot Father format` section of the topmost comment in that file to bot father to update the command list for your bot. Or you can copy it from the bot father command format section.

4. Compile the bot using `dotnet build` or `dotnet publish`

5. Export group chat history for your group with the following setting:
	* do not select to include any media file 
	* set `size limit` to max
	* select `Format` to `JSON`
	* set `From` to `the oldest message`
	* set `To` to `present`

6. Run the bot with `dotnet DidiIndexingBot[.dll|.exe] {databaseFilePath} {jsonRecordPath}` to import message archive, in which:
	* `databaseFilePath` is the SQLite database file, use `Database/emptyDB.db` as a template or you can use the commands in the SQL command section to create your own database
	* `jsonRecordPath` is the `result.json` you just exported from Telegram
	* Noted that both parameters use relative path ONLY

7. Add the bot to the group chat, noted that the group chat must have the same id as `groupId` in your user secret. One bot can only serve one group.

8. Run the bot with `dotnet DidiIndexingBot[.dll|.exe] {databaseFilePath}` to start the bot in regular mode.

9. Remember to back up the database regularly!

10. Profit.

## SQL Command to Create Database

To create main messages table:

```sql
CREATE TABLE messages 
(
    id                  BIGINT PRIMARY KEY
                               UNIQUE
                               NOT NULL,
    date                DATE   NOT NULL,
    from_name           TEXT   NOT NULL,
    from_id             BIGINT NOT NULL,
    text                TEXT,
    reply_to_message_id BIGINT,
    forwarded_from      TEXT
);
```

To create fts5 virtual table:

```sql
CREATE VIRTUAL TABLE search USING fts5(id, text);
```

## Available Bot Commands

Note the commands with `@BotName` must be called with bot name, other ones can ignore it.

`/search {term}` Search message that contains {term}

`/searchfts5 {term}` Search message that contains {term}, force FTS5

`/searchlike {term}` Search message that contains {term}, force LIKE

`/viewarchive {message id}` View archive for message with that id

`/random@BotName` Get a random quote

`/whosaid@BotName` Who said this random quote?

`/debug@BotName` Show debug message

## Bot Father Command Format

```
search		- /search {term}: Search message that contains {term}
searchfts5	- /searchfts5 {term}: Search message that contains {term}, force FTS5
searchlike	- /searchlike {term}: Search message that contains {term}, force LIKE
viewarchive	- /viewarchive {message id}: View archive for message with that id
random		- /random: Get a random quote
whosaid		- /whosaid: Who said this random quote? This command has a cooldown of 60s
debug		- /debug: debug message
```

## Note

* This bot is built using C# / .Net 6.0 and SQLite

* One bot can only serve one group

* This bot is not bug-free, use it is without any warranty