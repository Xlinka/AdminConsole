# AdminConsole

AdminConsole is a standalone console application designed to interface with the Resonite chat, primarily for the Resonite bot to execute commands without opening the game. This program allows users to log in, send commands, and receive messages from Resonite users.

## Features

- User authentication with support for 2FA.
- Command execution without opening the Resonite game.
- Logging of all activities to a file with timestamps.
- Real-time message reception and display.
- Ability to change the target user for messaging.

## Prerequisites

- .NET SDK
- SkyFrost library

## Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/yourusername/AdminConsole.git
    ```

2. Navigate to the project directory:
    ```sh
    cd AdminConsole
    ```

3. Restore the dependencies:
    ```sh
    dotnet restore
    ```

4. Build the project:
    ```sh
    dotnet build
    ```

## Usage

1. Run the application:
    ```sh
    dotnet run
    ```

2. Follow the prompts to log in with your Resonite credentials.

## Commands

- `/changeuser [user]` - Change the target user for messaging. Default is `U-Resonite`.
- `/exit` - Exit the application.

## Logging

All activities are logged to a file named `console_log_yyyyMMdd_HHmmss.txt` in the application directory. Each log entry is timestamped for debugging purposes.

## Example

```sh
Program started at 2024-06-12 15:03:34
Generated machine ID: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_
Generated UID: 1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF
Prompting for login...
Login: yourusername
Password: ********
Attempting to log in...
Login successful. Updating user status to Online...
Setting up message listener...
currentUsername> /changeuser U-NewUser
Changed current user to: U-NewUser
currentUsername> /exit
Exit command received. Canceling...
Logging out...
Program ended.
```