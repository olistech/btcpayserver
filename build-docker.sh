NAME=btcpayserver
docker build -t $NAME --file ./amd64.Dockerfile .
CO=$(aws ecr get-login --region eu-west-1 --no-include-email) && $CO
docker tag $NAME 793603699189.dkr.ecr.eu-west-1.amazonaws.com/$NAME:latest &&
docker push 793603699189.dkr.ecr.eu-west-1.amazonaws.com/$NAME:latest

