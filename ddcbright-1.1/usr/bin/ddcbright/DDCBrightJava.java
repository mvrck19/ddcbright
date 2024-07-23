import javax.swing.*;
import java.awt.*;
import java.awt.event.*;
import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;

public class DDCBrightJava extends JFrame {
    private JSlider brightnessSlider;

    public DDCBrightJava() {
        setTitle("DDCBright - Brightness Control");
        setSize(400, 100);
        setLocationRelativeTo(null);
        setDefaultCloseOperation(EXIT_ON_CLOSE);

        initUI();
    }

    private void initUI() {
        JPanel panel = new JPanel();
        getContentPane().add(panel);

        panel.setLayout(new BoxLayout(panel, BoxLayout.Y_AXIS));

        JLabel label = new JLabel("Adjust Brightness:");
        label.setAlignmentX(Component.CENTER_ALIGNMENT);
        panel.add(label);

        brightnessSlider = new JSlider(0, 100);
        brightnessSlider.setValue(getCurrentBrightness());
        brightnessSlider.addChangeListener(e -> setBrightness(brightnessSlider.getValue()));
        panel.add(brightnessSlider);
    }

    private int getCurrentBrightness() {
        try {
            Process process = Runtime.getRuntime().exec("ddcutil getvcp 10");
            BufferedReader reader = new BufferedReader(new InputStreamReader(process.getInputStream()));
            String line;
            while ((line = reader.readLine()) != null) {
                if (line.contains("current value =")) {
                    return Integer.parseInt(line.split("=")[1].trim());
                }
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
        return 50; // Default to 50 if unable to determine
    }

    private void setBrightness(int brightness) {
        try {
            Runtime.getRuntime().exec("ddcutil setvcp 10 " + brightness);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    public static void main(String[] args) {
        EventQueue.invokeLater(() -> {
            DDCBrightJava ex = new DDCBrightJava();
            ex.setVisible(true);
        });
    }
}