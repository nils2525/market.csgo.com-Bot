# market.csgo.com-BuyBot

A bot for market.csgo.com that automatically buy items under a specific price limit.

## Features

- Automatically buy items at a specific price
- Configure application using a config.json file
- Pause when steam inventory is full
- Different Buy-Modes
- Update configuration without restart

### Buy-Modes

- IgnoreAveragePrice
    - Ignore the average price, always buy when Price <= MaxPrice
    - Config value: **0**
- ConsiderAveragePrice
  - Use the average price when it is smaller than the configured MaxPrice
  - Config value: **1**
- UseAveragePrice
    - Ignore the configured MaxPrice, always buy when price is <= Average Price
    - Config value: **2**

## Installation

1. Compile using Visual Studio
2. Copy config template (config.json.example) to the build output path
3. Edit config
4. Run application as console

## Planned Features

- Stop buying when you have no money left
- Stop buying after a specific amount
- Use multiple currencies
