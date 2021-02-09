module Utils

open System

type UserData = {
    mutable userTweets: Set<string>
    mutable following: Set<string>
    mutable followers: Set<string>
}

type Tweets = {
    mutable userid: string
    text: string
    mutable time_of_tweet: DateTime // DateTime.Now to get current time
    mutable retweet: bool
}

type Mentions = {
    mutable mentionedTweets: Set<string>
}

type Hashtags = {
    mutable hashtaggedTweets: Set<string>
}

type Query =
| Following of string
| Hashtag of string
| Mention of string

type Messages =
| Register of string
| Success of string
| Tweet of string * string * DateTime * bool
| Follow of string * string
| Retweet of string * string
| Search of Query
| Response of Set<Tweets>
| Connect of string
| Disconnect of string

let mentionParser (message: string) = 
    let mutable mentions: Set<string> = Set.empty
    let words = message.Split ' '
    for word in words do
        if word.[0] = '@' then
            mentions <- mentions.Add(word.ToLower())
    mentions

let hashtagParser (message: string) = 
    let mutable hashtags: Set<string> = Set.empty
    let words = message.Split ' '
    for word in words do
        if word.[0] = '#' then
            hashtags <- hashtags.Add(word.ToLower())
    hashtags