# ddcbright - Brightness Control Application

This is a simple GUI application for controlling the brightness of your monitor on Linux. It uses the `ddcutil` command to get and set the brightness over the I2C bus.

## Features

- Get the current brightness of the monitor
- Set the brightness to a desired level
- Display the current brightness level in a user-friendly GUI

## Dependencies

- Python 3
- PyQt5 for the GUI
- `ddcutil` command line tool

## Usage

1. Clone the repository:
    ```bash
    git clone https://github.com/mvrck19/ddcbright.git
    cd ddcbright
    ```

2. Install the dependencies:
    ```bash
    pip install PyQt5
    sudo apt install ddcutil
    ```

3. Run the application:
    ```bash
    python app.py
    ```

## Troubleshooting

If you encounter any issues with the application, please check the following:

- Make sure your monitor supports DDC/CI and it is enabled. You can check this in your monitor's OSD menu.
- Make sure your user has the necessary permissions to access the I2C devices. You might need to add your user to the `i2c` group.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License.
