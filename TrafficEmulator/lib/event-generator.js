import encoding from 'k6/encoding';

const TCP_PROTOCOL = 6;
const UDP_PROTOCOL = 17;
const ICMP_PROTOCOL = 1;

const TCP_DESTINATION_PORTS = [80, 443, 8080, 8443, 9092];
const UDP_DESTINATION_PORTS = [53, 123, 443, 5353];

/** Возвращает случайное целое число в заданном диапазоне включительно. */
function randomInt(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

/** Возвращает UUID v4 для идентификации сетевого события. */
function createUuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (character) => {
    const random = randomInt(0, 15);
    const value = character === 'x' ? random : ((random & 0x03) | 0x08);
    return value.toString(16);
  });
}

/** Кодирует IPv4-адрес в base64-представление protobuf bytes. */
function encodeIpv4(octets) {
  return encoding.b64encode(new Uint8Array(octets).buffer);
}

/** Генерирует синтетический IPv4-адрес источника из частного диапазона 10.0.0.0/8. */
function createSourceIp() {
  return encodeIpv4([10, randomInt(0, 255), randomInt(0, 255), randomInt(1, 254)]);
}

/** Генерирует синтетический IPv4-адрес назначения из частного диапазона 172.16.0.0/12. */
function createDestinationIp() {
  return encodeIpv4([172, randomInt(16, 31), randomInt(0, 255), randomInt(1, 254)]);
}

/** Создаёт PacketEvent в формате ProtoJSON, который ожидает k6 gRPC-клиент. */
export function createPacketEvent() {
  const protocolRoll = randomInt(1, 100);
  const protocol = protocolRoll <= 5
    ? ICMP_PROTOCOL
    : protocolRoll <= 65
      ? TCP_PROTOCOL
      : UDP_PROTOCOL;

  const event = {
    eventId: createUuid(),
    sourceId: `sensor-${String((__VU % 32) + 1).padStart(3, '0')}`,
    occurredAtUtc: new Date().toISOString(),
    sourceIp: createSourceIp(),
    destinationIp: createDestinationIp(),
    protocol,
    packetLengthBytes: randomInt(64, 1500),
  };

  if (protocol === TCP_PROTOCOL) {
    event.sourcePort = randomInt(49152, 65535);
    event.destinationPort = TCP_DESTINATION_PORTS[randomInt(0, TCP_DESTINATION_PORTS.length - 1)];
  } else if (protocol === UDP_PROTOCOL) {
    event.sourcePort = randomInt(49152, 65535);
    event.destinationPort = UDP_DESTINATION_PORTS[randomInt(0, UDP_DESTINATION_PORTS.length - 1)];
  }

  return event;
}
