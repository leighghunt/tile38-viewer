version: '3'
services:
  tile38:
    image: tile38/tile38
    container_name: tile38
    environment:
    ports:
        - 9851:9851
    restart: unless-stopped
  tile38-viewer:
    image: leighghunt/tile38-viewer
    container_name: tile38-viewer
    build: ./src
    env_file: ./docker-environment-list
    depends_on:
        - tile38
    restart: unless-stopped
volumes:
  geoevent:
networks:
  default:
    external:
      name: nat
