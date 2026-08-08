/*
  Binary telemetry simulator for UyduArayuz_1.

  Connect Arduino over USB, select the Arduino COM port in the WPF app,
  and use the same baud rate as BAUD_RATE below.

  Do not use Serial.print/println on this serial port. The desktop app
  expects only 71-byte binary frames.
*/

#include <Arduino.h>

const uint32_t BAUD_RATE = 9600;
const uint8_t START_BYTE = 0x3C; // '<'
const uint8_t END_BYTE = 0x3E;   // '>'
const uint8_t PACKET_LENGTH = 71;

const uint8_t OFFSET_START = 0;
const uint8_t OFFSET_ADDRESS_HIGH = 1;
const uint8_t OFFSET_ADDRESS_LOW = 2;
const uint8_t OFFSET_CHANNEL = 3;
const uint8_t OFFSET_PACKET_NO = 4;
const uint8_t OFFSET_SATELLITE_STATUS = 6;
const uint8_t OFFSET_ERROR_CODE = 7;
const uint8_t OFFSET_SENT_TIME = 8;
const uint8_t OFFSET_PRESSURE = 12;
const uint8_t OFFSET_HEIGHT = 16;
const uint8_t OFFSET_LANDING_SPEED = 20;
const uint8_t OFFSET_TEMPERATURE = 24;
const uint8_t OFFSET_BATTERY_VOLTAGE = 28;
const uint8_t OFFSET_GPS_LATITUDE = 32;
const uint8_t OFFSET_GPS_LONGITUDE = 36;
const uint8_t OFFSET_GPS_ALTITUDE = 40;
const uint8_t OFFSET_PITCH = 44;
const uint8_t OFFSET_ROLL = 48;
const uint8_t OFFSET_YAW = 52;
const uint8_t OFFSET_TASK_CODE = 56;
const uint8_t OFFSET_TEAM_NO = 62;
const uint8_t OFFSET_CRC = 66;
const uint8_t OFFSET_END = 70;

const uint8_t CRC_START_OFFSET = OFFSET_PACKET_NO;
const uint8_t CRC_PAYLOAD_LENGTH = OFFSET_CRC - CRC_START_OFFSET;

uint16_t packetNo = 0;

// 22.06.2026 12:00:00 UTC. Arduino has no real clock, so millis() is added.
const uint32_t BASE_UNIX_TIME = 1782129600UL;

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
  uint32_t crc = 0xFFFFFFFFUL;

  for (uint8_t i = 0; i < length; i++) {
    crc ^= data[i];

    for (uint8_t bit = 0; bit < 8; bit++) {
      bool leastSignificantBitSet = (crc & 1UL) != 0;
      crc >>= 1;

      if (leastSignificantBitSet) {
        crc ^= 0xEDB88320UL;
      }
    }
  }

  return crc ^ 0xFFFFFFFFUL;
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

  frame[OFFSET_START] = START_BYTE;
  frame[OFFSET_ADDRESS_HIGH] = 0x00;
  frame[OFFSET_ADDRESS_LOW] = 0x01;
  frame[OFFSET_CHANNEL] = 0x0C;

  writeUInt16LE(frame, OFFSET_PACKET_NO, packetNo++);

  frame[OFFSET_SATELLITE_STATUS] = 3; // Ayrilma

  // Cycle through a few error states so the LED panel can be tested.
  frame[OFFSET_ERROR_CODE] = (packetNo / 10) % 16;

  writeUInt32LE(frame, OFFSET_SENT_TIME, BASE_UNIX_TIME + (millis() / 1000UL));

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

  frame[OFFSET_END] = END_BYTE;
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
