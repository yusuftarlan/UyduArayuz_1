/*
  Binary telemetry simulator for UyduArayuz_1.

  Connect Arduino over USB, select the Arduino COM port in the WPF app,
  and use the same baud rate as BAUD_RATE below.

  Do not use Serial.print/println on this serial port. The desktop app
  expects only 80-byte binary frames.
*/

#include <Arduino.h>

const uint32_t BAUD_RATE = 9600;
const uint32_t PACKET_START = 0x3C3C3C3CUL; // "<<<<"
const uint32_t PACKET_END = 0x3E3E3E3EUL;   // ">>>>"
const uint8_t PACKET_LENGTH = 80;

const uint8_t OFFSET_START = 0;
const uint8_t OFFSET_PACKET_NO = 4;
const uint8_t OFFSET_SATELLITE_STATUS = 8;
const uint8_t OFFSET_ERROR_CODE = 10;
const uint8_t OFFSET_RTC_YEAR = 12;
const uint8_t OFFSET_RTC_MONTH = 13;
const uint8_t OFFSET_RTC_DAY = 14;
const uint8_t OFFSET_RTC_HOUR = 15;
const uint8_t OFFSET_RTC_MINUTE = 16;
const uint8_t OFFSET_RTC_SECOND = 17;
const uint8_t OFFSET_PRESSURE = 18;
const uint8_t OFFSET_HEIGHT = 22;
const uint8_t OFFSET_LANDING_SPEED = 26;
const uint8_t OFFSET_TEMPERATURE = 30;
const uint8_t OFFSET_BATTERY_VOLTAGE = 34;
const uint8_t OFFSET_GPS_LATITUDE = 38;
const uint8_t OFFSET_GPS_LONGITUDE = 42;
const uint8_t OFFSET_GPS_ALTITUDE = 46;
const uint8_t OFFSET_PITCH = 50;
const uint8_t OFFSET_ROLL = 54;
const uint8_t OFFSET_YAW = 58;
const uint8_t OFFSET_TASK_CODE = 62;
const uint8_t OFFSET_TEAM_NO = 68;
const uint8_t OFFSET_CRC = 72;
const uint8_t OFFSET_END = 76;

const uint8_t CRC_START_OFFSET = OFFSET_START;
const uint8_t CRC_PAYLOAD_LENGTH = OFFSET_CRC - CRC_START_OFFSET;

uint32_t packetNo = 0;

void writeUInt16LE(uint8_t* buffer, uint8_t offset, uint16_t value) {
  buffer[offset] = value & 0xFF;
  buffer[offset + 1] = (value >> 8) & 0xFF;
}

void writeUInt32LE(uint8_t* buffer, uint8_t offset, uint32_t value) {
  buffer[offset] = value & 0xFF;
  buffer[offset + 1] = (value >> 8) & 0xFF;
  buffer[offset + 2] = (value >> 16) & 0xFF;
  buffer[offset + 3] = (value >> 24) & 0xFF;
}

uint32_t computeCrc32(const uint8_t* data, uint8_t length) {
  if ((length % 4) != 0) {
    return 0;
  }

  uint32_t crc = 0xFFFFFFFFUL;

  for (uint8_t i = 0; i < length; i += 4) {
    uint32_t word =
      ((uint32_t)data[i]) |
      ((uint32_t)data[i + 1] << 8) |
      ((uint32_t)data[i + 2] << 16) |
      ((uint32_t)data[i + 3] << 24);

    crc ^= word;

    for (uint8_t bit = 0; bit < 32; bit++) {
      crc = (crc & 0x80000000UL)
        ? (crc << 1) ^ 0x04C11DB7UL
        : crc << 1;
    }
  }

  return crc;
}

void writeFloatLE(uint8_t* buffer, uint8_t offset, float value) {
  union {
    float f;
    uint8_t b[4];
  } converter;

  converter.f = value;
  buffer[offset] = converter.b[0];
  buffer[offset + 1] = converter.b[1];
  buffer[offset + 2] = converter.b[2];
  buffer[offset + 3] = converter.b[3];
}

void writeTaskCode(uint8_t* buffer, const char* taskCode) {
  for (uint8_t i = 0; i < 6; i++) {
    buffer[OFFSET_TASK_CODE + i] = taskCode[i];
  }
}

void buildTelemetryFrame(uint8_t* frame) {
  memset(frame, 0, PACKET_LENGTH);

  float t = millis() / 1000.0f;
  uint32_t elapsedSeconds = millis() / 1000UL;

  writeUInt32LE(frame, OFFSET_START, PACKET_START);

  writeUInt32LE(frame, OFFSET_PACKET_NO, packetNo++);

  writeUInt16LE(frame, OFFSET_SATELLITE_STATUS, 3); // Ayrilma

  // Cycle through a few error states so the LED panel can be tested.
  writeUInt16LE(frame, OFFSET_ERROR_CODE, (packetNo / 10) % 16);

  frame[OFFSET_RTC_YEAR] = 26;
  frame[OFFSET_RTC_MONTH] = 6;
  frame[OFFSET_RTC_DAY] = 22;
  frame[OFFSET_RTC_HOUR] = (12 + (elapsedSeconds / 3600UL)) % 24;
  frame[OFFSET_RTC_MINUTE] = (elapsedSeconds / 60UL) % 60;
  frame[OFFSET_RTC_SECOND] = elapsedSeconds % 60;

  writeFloatLE(frame, OFFSET_PRESSURE, 101.33f + sin(t * 0.35f) * 2.0f);
  writeFloatLE(frame, OFFSET_HEIGHT, 530.12f + t * 1.5f);
  writeFloatLE(frame, OFFSET_LANDING_SPEED, 11.32f + sin(t * 0.50f));
  writeFloatLE(frame, OFFSET_TEMPERATURE, 26.11f + sin(t * 0.20f) * 3.0f);
  writeFloatLE(frame, OFFSET_BATTERY_VOLTAGE, 21.34f - min(t * 0.01f, 2.0f));

  writeFloatLE(frame, OFFSET_GPS_LATITUDE, 39.9250f + sin(t * 0.05f) * 0.001f);
  writeFloatLE(frame, OFFSET_GPS_LONGITUDE, 32.8369f + cos(t * 0.05f) * 0.001f);
  writeFloatLE(frame, OFFSET_GPS_ALTITUDE, 32.23f + t * 0.4f);

  writeFloatLE(frame, OFFSET_PITCH, sin(t * 0.70f) * 20.0f);
  writeFloatLE(frame, OFFSET_ROLL, cos(t * 0.60f) * 20.0f);
  writeFloatLE(frame, OFFSET_YAW, fmod(t * 15.0f, 360.0f));

  writeTaskCode(frame, "2R0B1G");
  writeUInt32LE(frame, OFFSET_TEAM_NO, 21325UL);

  uint32_t crc = computeCrc32(&frame[CRC_START_OFFSET], CRC_PAYLOAD_LENGTH);
  writeUInt32LE(frame, OFFSET_CRC, crc);

  writeUInt32LE(frame, OFFSET_END, PACKET_END);
}

void setup() {
  Serial.begin(BAUD_RATE);
}

void loop() {
  uint8_t frame[PACKET_LENGTH];
  buildTelemetryFrame(frame);

  Serial.write(frame, PACKET_LENGTH);
  Serial.flush();

  delay(1000);
}
